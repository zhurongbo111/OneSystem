using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.PartnerPrices.GetPartnerPrices;

/// <summary>
/// 客户价格分页查询用例：委托协议价仓储做联查分页 → 映射列表出参
/// </summary>
public sealed class GetPartnerPricesRequestHandler : IRequestHandler<GetPartnerPricesRequest, PagedResult<PartnerPriceListItemDto>>
{
    private readonly IPartnerPriceRepository _partnerPriceRepository;

    /// <summary>
    /// 初始化客户价格分页查询用例处理器
    /// </summary>
    public GetPartnerPricesRequestHandler(IPartnerPriceRepository partnerPriceRepository)
    {
        _partnerPriceRepository = partnerPriceRepository;
    }

    /// <summary>
    /// 处理客户价格分页查询请求
    /// </summary>
    /// <param name="request">查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<PartnerPriceListItemDto>> HandleAsync(GetPartnerPricesRequest request, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _partnerPriceRepository.GetPagedAsync(
            request.PartnerId, request.ProductId, request.Keyword, request.Page, request.PageSize, cancellationToken);

        return new PagedResult<PartnerPriceListItemDto>
        {
            Items = items.Select(PartnerPricesDtoMapper.ToPartnerPriceListItemDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}