using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.PartnerPrices.UpdatePartnerPrice;

/// <summary>
/// 编辑客户协议价用例：取记录 → 更新价与备注（客户 / 商品保持原值）→ 落库 + 审计。
/// 备注按全量覆盖语义处理：缺字段 / 空串 / 纯空白一律清空（AGENTS.md §4.5）。
/// </summary>
public sealed class UpdatePartnerPriceRequestHandler : IRequestHandler<UpdatePartnerPriceRequest, PartnerPriceDetailDto>
{
    private readonly IPartnerPriceRepository _partnerPriceRepository;
    private readonly IPartnerRepository _partnerRepository;
    private readonly IProductRepository _productRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化编辑客户协议价用例处理器
    /// </summary>
    public UpdatePartnerPriceRequestHandler(
        IPartnerPriceRepository partnerPriceRepository,
        IPartnerRepository partnerRepository,
        IProductRepository productRepository,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _partnerPriceRepository = partnerPriceRepository;
        _partnerRepository = partnerRepository;
        _productRepository = productRepository;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理编辑客户协议价请求
    /// </summary>
    /// <param name="request">编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PartnerPriceDetailDto> HandleAsync(UpdatePartnerPriceRequest request, CancellationToken cancellationToken = default)
    {
        var partnerPrice = await _partnerPriceRepository.GetByIdAsync(request.Id, cancellationToken);
        if (partnerPrice is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "客户协议价不存在");
        }

        var beforePrice = partnerPrice.Price;
        var beforeRemark = partnerPrice.Remark;
        var now = DateTimeOffset.UtcNow;

        partnerPrice.Price = request.Price;
        partnerPrice.Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();
        partnerPrice.UpdatedAt = now;
        partnerPrice.UpdatedBy = _currentUser.UserId();

        await _partnerPriceRepository.UpdateAsync(partnerPrice, cancellationToken);

        var partner = await _partnerRepository.GetByIdAsync(partnerPrice.PartnerId, cancellationToken);
        var product = await _productRepository.GetByIdAsync(partnerPrice.ProductId, cancellationToken);
        if (partner is null || product is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "客户协议价关联的客户或商品不存在");
        }

        var changeBuilder = new AuditChangeBuilder()
            .Add("price", "协议单价", AuditSummary.Money(beforePrice), AuditSummary.Money(partnerPrice.Price))
            .Add("remark", "备注", beforeRemark, partnerPrice.Remark);
        await _auditLogger.RecordAsync(new AuditEntry
        {
            Resource = AuditResource.PartnerPrice,
            Action = AuditAction.Update,
            ResourceId = partnerPrice.Id,
            ResourceNo = $"{partner.Name} / {product.Code}",
            Summary = $"编辑客户协议价 {partner.Name} / {product.Code}",
            Changes = changeBuilder.Build(),
            ChangesTruncated = changeBuilder.Truncated,
            UtcNow = now,
        }, cancellationToken);

        return PartnerPricesDtoMapper.ToPartnerPriceDetailDto(partnerPrice, partner.Name, product);
    }
}