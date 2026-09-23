using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.PartnerPrices.GetPartnerPrices;

/// <summary>
/// 客户价格分页查询请求（客户 / 商品 / 关键词筛选）
/// </summary>
public sealed class GetPartnerPricesRequest : IRequest<PagedResult<PartnerPriceListItemDto>>
{
    /// <summary>页码，从 1 起</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; init; } = 20;

    /// <summary>关键词，可空（客户名 / 商品编码 / 商品名称）</summary>
    public string? Keyword { get; init; }

    /// <summary>客户 id，可空</summary>
    public Guid? PartnerId { get; init; }

    /// <summary>商品 id，可空</summary>
    public Guid? ProductId { get; init; }
}