using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.PartnerPrices.GetEffectivePrices;

/// <summary>
/// 批量取生效价用例：校验商品都存在（缺失 → 40400）→ 仓储按「协议价优先」批量取价 → 映射出参。
/// 优先级口径见 specs/036-erp-partner-price/design.md §0.1；本用例只读，不写日志（审计只覆盖写操作）。
/// </summary>
public sealed class GetEffectivePricesRequestHandler : IRequestHandler<GetEffectivePricesRequest, IReadOnlyList<EffectivePriceDto>>
{
    private readonly IPartnerPriceRepository _partnerPriceRepository;
    private readonly IProductRepository _productRepository;

    /// <summary>
    /// 初始化批量取生效价用例处理器
    /// </summary>
    public GetEffectivePricesRequestHandler(
        IPartnerPriceRepository partnerPriceRepository,
        IProductRepository productRepository)
    {
        _partnerPriceRepository = partnerPriceRepository;
        _productRepository = productRepository;
    }

    /// <summary>
    /// 处理批量取生效价请求
    /// </summary>
    /// <param name="request">取价请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<IReadOnlyList<EffectivePriceDto>> HandleAsync(GetEffectivePricesRequest request, CancellationToken cancellationToken = default)
    {
        var productIds = request.ProductIds.Distinct().ToList();

        // 存在性校验（停用的商品照常返回价格，由前端商品下拉负责过滤）
        var codes = await _productRepository.GetCodesByIdsAsync(productIds, cancellationToken);
        if (codes.Count != productIds.Count)
        {
            throw new BusinessException(ErrorCode.NotFound, "商品不存在");
        }

        var items = await _partnerPriceRepository.GetEffectiveAsync(request.PartnerId, productIds, cancellationToken);

        return items.Select(PartnerPricesDtoMapper.ToEffectivePriceDto).ToList();
    }
}