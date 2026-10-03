using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Batches.UpdateBatchStatus;

/// <summary>
/// 批次停用 / 启用用例：取批次（40400）→ 置状态（停用后不可用于新单）→ 审计
/// </summary>
public sealed class UpdateBatchStatusRequestHandler : IRequestHandler<UpdateBatchStatusRequest, BatchDetailDto>
{
    private readonly IBatchRepository _batchRepository;
    private readonly IProductRepository _productRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化批次停用 / 启用用例处理器
    /// </summary>
    public UpdateBatchStatusRequestHandler(
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
    /// 处理批次停用 / 启用请求
    /// </summary>
    /// <param name="request">停用 / 启用请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<BatchDetailDto> HandleAsync(UpdateBatchStatusRequest request, CancellationToken cancellationToken = default)
    {
        var batch = await _batchRepository.GetByIdAsync(request.Id, cancellationToken);
        if (batch is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "批次不存在");
        }

        var newStatus = (PartnerStatus)request.Status;
        var beforeStatus = batch.Status;
        var now = DateTimeOffset.UtcNow;
        batch.Status = newStatus;
        batch.UpdatedAt = now;
        batch.UpdatedBy = _currentUser.UserId();

        await _batchRepository.UpdateAsync(batch, cancellationToken);

        var product = await _productRepository.GetByIdAsync(batch.ProductId, cancellationToken);

        var changeBuilder = new AuditChangeBuilder()
            .Add("status", "状态", AuditText.PartnerStatus(beforeStatus), AuditText.PartnerStatus(newStatus));
        await _auditLogger.RecordAsync(new AuditEntry
        {
            Resource = AuditResource.Batch,
            Action = AuditAction.StatusChange,
            ResourceId = batch.Id,
            ResourceNo = batch.BatchNo,
            Summary = $"{AuditText.PartnerStatus(newStatus)}批次 {batch.BatchNo}（{product?.Code} {product?.Name}）",
            Changes = changeBuilder.Build(),
            ChangesTruncated = changeBuilder.Truncated,
            UtcNow = now,
        }, cancellationToken);

        return BatchDtoMapper.ToDetailDto(batch, product?.Code, product?.Name);
    }
}
