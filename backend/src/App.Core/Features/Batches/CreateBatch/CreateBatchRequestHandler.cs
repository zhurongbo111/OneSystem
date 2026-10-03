using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Batches.CreateBatch;

/// <summary>
/// 新增批次用例：取商品（不存在 → 40400；未启用批次管理 → 40000）→ 批次号唯一（大小写不敏感 → 40129）→ 落库（默认启用）+ 审计。
/// 单一仓储写由仓储自身持久化，无需 IUnitOfWork。
/// </summary>
public sealed class CreateBatchRequestHandler : IRequestHandler<CreateBatchRequest, BatchDetailDto>
{
    private readonly IBatchRepository _batchRepository;
    private readonly IProductRepository _productRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化新增批次用例处理器
    /// </summary>
    public CreateBatchRequestHandler(
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
    /// 处理新增批次请求
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<BatchDetailDto> HandleAsync(CreateBatchRequest request, CancellationToken cancellationToken = default)
    {
        var batchNo = request.BatchNo.Trim();

        // 取商品：不存在 → 40400；未启用批次管理 → 40000（040 §3.4）
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "商品不存在");
        }

        if (!product.IsBatchManaged)
        {
            throw new BusinessException(ErrorCode.Validation, "该商品未启用批次管理");
        }

        // 查库约束：同商品内批次号唯一（大小写不敏感）
        if (await _batchRepository.ExistsByBatchNoAsync(request.ProductId, batchNo, null, cancellationToken))
        {
            throw new BusinessException(ErrorCode.BatchNoExists, "该商品下批次号已存在");
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();
        var batch = new Batch
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            BatchNo = batchNo,
            // 日期统一按 UTC 午夜存储（保质期到日不到时，040 §5）。
            // 不能直接 ?.Date：DateTimeOffset.Date 返回 Kind=Unspecified 的 DateTime，
            // 赋给 DateTimeOffset? 属性时隐式转换按本地时区解释（偏移漂移）→ Npgsql 写 timestamptz 报 50000
            ProductionDate = BatchFieldConstraints.NormalizeToDateUtcMidnight(request.ProductionDate),
            ExpiryDate = BatchFieldConstraints.NormalizeToDateUtcMidnight(request.ExpiryDate),
            Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim(),
            Status = PartnerStatus.Enabled,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        await _batchRepository.AddAsync(batch, cancellationToken);

        var changeBuilder = new AuditChangeBuilder()
            .Add("productId", "商品", null, $"{product.Code} {product.Name}")
            .Add("batchNo", "批次号", null, batch.BatchNo)
            .Add("productionDate", "生产日期", null, batch.ProductionDate?.ToString("yyyy-MM-dd"))
            .Add("expiryDate", "到期日", null, batch.ExpiryDate?.ToString("yyyy-MM-dd"))
            .Add("remark", "备注", null, batch.Remark);
        await _auditLogger.RecordAsync(new AuditEntry
        {
            Resource = AuditResource.Batch,
            Action = AuditAction.Create,
            ResourceId = batch.Id,
            ResourceNo = batch.BatchNo,
            Summary = $"新增批次 {batch.BatchNo}（{product.Code} {product.Name}）",
            Changes = changeBuilder.Build(),
            ChangesTruncated = changeBuilder.Truncated,
            UtcNow = now,
        }, cancellationToken);

        return BatchDtoMapper.ToDetailDto(batch, product.Code, product.Name);
    }
}
