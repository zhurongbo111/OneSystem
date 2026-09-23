using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.PartnerPrices.DeletePartnerPrice;

/// <summary>
/// 删除客户协议价用例：取记录 → 删除 → 审计（历史单据单价已是快照，无引用风险）
/// </summary>
public sealed class DeletePartnerPriceRequestHandler : IRequestHandler<DeletePartnerPriceRequest, object?>
{
    private readonly IPartnerPriceRepository _partnerPriceRepository;
    private readonly IPartnerRepository _partnerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化删除客户协议价用例处理器
    /// </summary>
    public DeletePartnerPriceRequestHandler(
        IPartnerPriceRepository partnerPriceRepository,
        IPartnerRepository partnerRepository,
        IProductRepository productRepository,
        IAuditLogger auditLogger)
    {
        _partnerPriceRepository = partnerPriceRepository;
        _partnerRepository = partnerRepository;
        _productRepository = productRepository;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理删除客户协议价请求
    /// </summary>
    /// <param name="request">删除请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<object?> HandleAsync(DeletePartnerPriceRequest request, CancellationToken cancellationToken = default)
    {
        var partnerPrice = await _partnerPriceRepository.GetByIdAsync(request.Id, cancellationToken);
        if (partnerPrice is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "客户协议价不存在");
        }

        var partner = await _partnerRepository.GetByIdAsync(partnerPrice.PartnerId, cancellationToken);
        var product = await _productRepository.GetByIdAsync(partnerPrice.ProductId, cancellationToken);

        await _partnerPriceRepository.DeleteAsync(request.Id, cancellationToken);

        var resourceNo = partner is not null && product is not null ? $"{partner.Name} / {product.Code}" : string.Empty;
        await _auditLogger.RecordAsync(new AuditEntry
        {
            Resource = AuditResource.PartnerPrice,
            Action = AuditAction.Delete,
            ResourceId = request.Id,
            ResourceNo = resourceNo,
            Summary = $"删除客户协议价 {resourceNo}（{AuditSummary.Money(partnerPrice.Price)}）",
            Changes = null,
            ChangesTruncated = false,
            UtcNow = DateTimeOffset.UtcNow,
        }, cancellationToken);

        return null;
    }
}