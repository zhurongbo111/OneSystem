using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.PartnerPrices.CreatePartnerPrice;

/// <summary>
/// 新增客户协议价用例：校验客户（存在 / 未停用 / 类型含客户）与商品（存在 / 未停用）→ 唯一性 → 落库 + 审计。
/// 供应商不可配置协议价（采购价协议属范围外），见 design.md §2.1。
/// </summary>
public sealed class CreatePartnerPriceRequestHandler : IRequestHandler<CreatePartnerPriceRequest, PartnerPriceDetailDto>
{
    private readonly IPartnerPriceRepository _partnerPriceRepository;
    private readonly IPartnerRepository _partnerRepository;
    private readonly IProductRepository _productRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化新增客户协议价用例处理器
    /// </summary>
    public CreatePartnerPriceRequestHandler(
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
    /// 处理新增客户协议价请求
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PartnerPriceDetailDto> HandleAsync(CreatePartnerPriceRequest request, CancellationToken cancellationToken = default)
    {
        var partner = await _partnerRepository.GetByIdAsync(request.PartnerId, cancellationToken);
        if (partner is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "往来单位不存在");
        }

        if (partner.Status == PartnerStatus.Disabled)
        {
            throw new BusinessException(ErrorCode.PartnerDisabled, "往来单位已停用");
        }

        if (partner.Type != PartnerType.Customer && partner.Type != PartnerType.Both)
        {
            throw new BusinessException(ErrorCode.Validation, "仅客户可配置协议价");
        }

        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "商品不存在");
        }

        if (product.Status == ProductStatus.Disabled)
        {
            throw new BusinessException(ErrorCode.ProductDisabled, "商品已停用");
        }

        if (await _partnerPriceRepository.ExistsAsync(request.PartnerId, request.ProductId, null, cancellationToken))
        {
            throw new BusinessException(ErrorCode.PartnerPriceExists, "该客户 + 商品的协议价已存在");
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();
        var remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();
        var partnerPrice = new PartnerPrice
        {
            Id = Guid.NewGuid(),
            PartnerId = request.PartnerId,
            ProductId = request.ProductId,
            Price = request.Price,
            Remark = remark,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        await _partnerPriceRepository.AddAsync(partnerPrice, cancellationToken);

        var changeBuilder = new AuditChangeBuilder()
            .Add("partnerId", "客户", null, partner.Name)
            .Add("productId", "商品", null, $"{product.Code} {product.Name}")
            .Add("price", "协议单价", null, AuditSummary.Money(partnerPrice.Price))
            .Add("remark", "备注", null, partnerPrice.Remark);
        await _auditLogger.RecordAsync(new AuditEntry
        {
            Resource = AuditResource.PartnerPrice,
            Action = AuditAction.Create,
            ResourceId = partnerPrice.Id,
            ResourceNo = $"{partner.Name} / {product.Code}",
            Summary = $"新增客户协议价 {partner.Name} / {product.Code}（{AuditSummary.Money(partnerPrice.Price)}）",
            Changes = changeBuilder.Build(),
            ChangesTruncated = changeBuilder.Truncated,
            UtcNow = now,
        }, cancellationToken);

        return PartnerPricesDtoMapper.ToPartnerPriceDetailDto(partnerPrice, partner.Name, product);
    }
}