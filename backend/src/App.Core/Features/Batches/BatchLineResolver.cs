using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Batches;

/// <summary>
/// 明细行批次解析结果（既有批次或就地新建批次）
/// </summary>
/// <param name="BatchId">批次 id</param>
/// <param name="BatchNo">批次号（快照存入单据明细）</param>
public sealed record ResolvedBatch(Guid BatchId, string BatchNo);

/// <summary>
/// 单据明细行批次解析（040-erp-batch-expiry/design.md §3.4，判定集中，五类单据 + 调拨共用）：
/// - 按批次商品：缺批次 → 40127；批次不存在 / 不属于该商品 → 40400；停用 → 40000；
///   出库类过期（ExpiryDate &lt; 今天）→ 40128（message 含批次号与到期日）；
/// - 非按批次商品传批次 → 40000（防串数据）；
/// - 就地新建批次（采购入库 / 销售退货）：newBatchNo 代替 batchId，同事务内先建批次再使用（重复 → 40129）。
/// </summary>
public static class BatchLineResolver
{
    /// <summary>
    /// 解析单据明细行的批次（每行调用一次；<c>batchId</c> 与 <c>newBatchNo</c> 不可同时提供 → 40000）
    /// </summary>
    /// <param name="isBatchManaged">商品是否按批次管理</param>
    /// <param name="productId">商品 id</param>
    /// <param name="batchId">明细指定的批次 id（可空）</param>
    /// <param name="newBatchNo">就地新建批次号（可空）</param>
    /// <param name="newProductionDate">就地新建批次的生产日期（可空）</param>
    /// <param name="newExpiryDate">就地新建批次的到期日（可空）</param>
    /// <param name="batchRepository">批次仓储</param>
    /// <param name="outbound">是否出库类单据（出库类拦截过期批次；入库类不拦）</param>
    /// <param name="today">「今天」（UTC 日期粒度，由 Handler 注入，过期判定用）</param>
    /// <param name="operatorId">操作人 id（就地新建批次时记录）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>解析后的批次；非按批次商品且未传批次时返回 null</returns>
    public static async Task<ResolvedBatch?> ResolveAsync(
        bool isBatchManaged,
        Guid productId,
        Guid? batchId,
        string? newBatchNo,
        DateTimeOffset? newProductionDate,
        DateTimeOffset? newExpiryDate,
        IBatchRepository batchRepository,
        bool outbound,
        DateTimeOffset today,
        Guid? operatorId,
        CancellationToken cancellationToken = default)
    {
        var hasNewBatch = !string.IsNullOrWhiteSpace(newBatchNo);

        // 双保险：batchId 与 newBatchNo 互斥（Validator 已拦格式层，此处兜底）
        if (batchId is not null && hasNewBatch)
        {
            throw new BusinessException(ErrorCode.Validation, "批次 id 与就地新建批次号不可同时提供");
        }

        // 非按批次商品：传了批次即串数据
        if (!isBatchManaged)
        {
            if (batchId is not null || hasNewBatch)
            {
                throw new BusinessException(ErrorCode.Validation, "该商品未启用批次管理，不可指定批次");
            }

            return null;
        }

        // 按批次商品：就地新建批次
        if (hasNewBatch)
        {
            // hasNewBatch 已保证非空且非空白
            return await CreateNewAsync(
                productId,
                newBatchNo!.Trim(),
                // 不能直接 ?.Date：DateTimeOffset.Date 返回 Kind=Unspecified 的 DateTime，
                // 赋给 DateTimeOffset? 时隐式转换按本地时区解释（偏移漂移）→ Npgsql 写 timestamptz 报 50000
                BatchFieldConstraints.NormalizeToDateUtcMidnight(newProductionDate),
                BatchFieldConstraints.NormalizeToDateUtcMidnight(newExpiryDate),
                batchRepository,
                operatorId,
                cancellationToken);
        }

        // 按批次商品：指定既有批次
        if (batchId is not null)
        {
            var batch = await batchRepository.GetByIdAsync(batchId.Value, cancellationToken);
            if (batch is null)
            {
                throw new BusinessException(ErrorCode.NotFound, "批次不存在");
            }

            if (batch.ProductId != productId)
            {
                throw new BusinessException(ErrorCode.NotFound, "批次不属于该商品");
            }

            if (batch.Status == PartnerStatus.Disabled)
            {
                throw new BusinessException(ErrorCode.Validation, $"批次 {batch.BatchNo} 已停用");
            }

            // 过期拦截（仅出库类；日期粒度：到期日当天仍可用，次日起过期）
            if (outbound
                && batch.ExpiryDate is not null
                && batch.ExpiryDate.Value < today)
            {
                throw new BusinessException(
                    ErrorCode.BatchExpired,
                    $"批次 {batch.BatchNo} 已于 {batch.ExpiryDate.Value:yyyy-MM-dd} 过期，禁止出库");
            }

            return new ResolvedBatch(batch.Id, batch.BatchNo);
        }

        // 按批次商品必须指定批次
        throw new BusinessException(ErrorCode.BatchRequired, "该商品按批次管理，明细行必须指定批次");
    }

    /// <summary>
    /// 就地新建批次（同一事务内调用，失败随事务回滚）：唯一性（大小写不敏感 → 40129）+ 日期先后（→ 40000）
    /// </summary>
    private static async Task<ResolvedBatch> CreateNewAsync(
        Guid productId,
        string batchNo,
        DateTimeOffset? productionDate,
        DateTimeOffset? expiryDate,
        IBatchRepository batchRepository,
        Guid? operatorId,
        CancellationToken cancellationToken)
    {
        if (expiryDate is not null && productionDate is not null && expiryDate.Value < productionDate.Value)
        {
            throw new BusinessException(ErrorCode.Validation, "到期日不能早于生产日期");
        }

        // 幂等：单号冲突重试（单据 Handler 的重试循环）时批次可能已随前一轮事务持久化，复用而非报重复
        var existing = await batchRepository.GetByProductAndBatchNoAsync(productId, batchNo, cancellationToken);
        if (existing is not null)
        {
            return new ResolvedBatch(existing.Id, existing.BatchNo);
        }

        if (await batchRepository.ExistsByBatchNoAsync(productId, batchNo, null, cancellationToken))
        {
            throw new BusinessException(ErrorCode.BatchNoExists, $"该商品下批次号 {batchNo} 已存在");
        }

        var now = DateTimeOffset.UtcNow;
        var batch = new Batch
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            BatchNo = batchNo,
            ProductionDate = productionDate,
            ExpiryDate = expiryDate,
            Remark = null,
            Status = PartnerStatus.Enabled,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        await batchRepository.AddAsync(batch, cancellationToken);
        return new ResolvedBatch(batch.Id, batch.BatchNo);
    }
}
