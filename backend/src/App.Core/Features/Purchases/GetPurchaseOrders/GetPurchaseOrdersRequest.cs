using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Responses;

namespace App.Core.Features.Purchases.GetPurchaseOrders;

/// <summary>
/// 采购单分页查询请求（Query 参数绑定；只读）
/// </summary>
public sealed class GetPurchaseOrdersRequest : IRequest<PagedResult<PurchaseOrderListItemDto>>
{
    /// <summary>页码，从 1 起</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; init; } = 20;

    /// <summary>单号 / 供应商名称关键词，可空</summary>
    public string? Keyword { get; init; }

    /// <summary>供应商 id，可空</summary>
    public Guid? PartnerId { get; init; }

    /// <summary>起始业务日期（含），可空（前端传本地当天 00:00 的 UTC ISO 串）</summary>
    public DateTimeOffset? Start { get; init; }

    /// <summary>结束业务日期（含），可空（前端传本地当天 23:59:59 的 UTC ISO 串）</summary>
    public DateTimeOffset? End { get; init; }

    /// <summary>结算状态（0 未付 / 1 已付），可空</summary>
    public OrderSettlementStatus? Settlement { get; init; }
}
