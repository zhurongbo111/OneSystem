using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Batches.UpdateBatch;

/// <summary>
/// 编辑批次用例：取批次（40400）→ 全量覆盖生产日期 / 到期日 / 备注（**批次号不可改**）→ 审计。
/// 单一仓储写由仓储自身持久化，无需 IUnitOfWork。
/// </summary>
public sealed class UpdateBatchRequestHandler : IRequestHandler<UpdateBatchRequest, BatchDetailDto>
{
    private readonly IBatchRepository _batchRepository;
    private readonly IProductRepository _productRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化编辑批次用例处理器
    /// </summary>
    public UpdateBatchRequestHandler(
        IBatchRepository batchRepository,
        IProductRepository productRepository,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _batchRepository = batchRepository;
        _productRepository = productRepository;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理编辑批次请求
    /// </summary>
    /// <param name="request">编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<BatchDetailDto> HandleAsync(UpdateBatchRequest request, CancellationToken cancellationToken = default)
    {
        var batch = await _batchRepository.GetByIdAsync(request.Id, cancellationToken);
        if (batch is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "批次不存在");
        }

        // 记录变更前值（审计 before；同 UpdatePartner 惯例）
        var beforeProductionDate = batch.ProductionDate;
        var beforeExpiryDate = batch.ExpiryDate;
        var beforeRemark = batch.Remark;

        // 全量覆盖（AGENTS.md §4.5）：空值清空；日期按 UTC 午夜存储。
        // 不能直接 ?.Date：DateTimeOffset.Date 返回 Kind=Unspecified 的 DateTime，
        // 赋给 DateTimeOffset? 属性时隐式转换按本地时区解释（偏移漂移）→ Npgsql 写 timestamptz 报 50000
        var newProductionDate = BatchFieldConstraints.NormalizeToDateUtcMidnight(request.ProductionDate);
        var newExpiryDate = BatchFieldConstraints.NormalizeToDateUtcMidnight(request.ExpiryDate);
        var newRemark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();

        var now = DateTimeOffset.UtcNow;
        batch.ProductionDate = newProductionDate;
        batch.ExpiryDate = newExpiryDate;
        batch.Remark = newRemark;
        batch.UpdatedAt = now;
        batch.UpdatedBy = _currentUser.UserId();

        await _batchRepository.UpdateAsync(batch, cancellationToken);

        var product = await _productRepository.GetByIdAsync(batch.ProductId, cancellationToken);

        var changeBuilder = new AuditChangeBuilder()
            .Add("productionDate", "生产日期", beforeProductionDate?.ToString("yyyy-MM-dd"), newProductionDate?.ToString("yyyy-MM-dd"))
            .Add("expiryDate", "到期日", beforeExpiryDate?.ToString("yyyy-MM-dd"), newExpiryDate?.ToString("yyyy-MM-dd"))
            .Add("remark", "备注", beforeRemark, newRemark);
        await _auditLogger.RecordAsync(new AuditEntry
        {
            Resource = AuditResource.Batch,
            Action = AuditAction.Update,
            ResourceId = batch.Id,
            ResourceNo = batch.BatchNo,
            Summary = $"编辑批次 {batch.BatchNo}（{product?.Code} {product?.Name}）",
            Changes = changeBuilder.Build(),
            ChangesTruncated = changeBuilder.Truncated,
            UtcNow = now,
        }, cancellationToken);

        return BatchDtoMapper.ToDetailDto(batch, product?.Code, product?.Name);
    }
}
