using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Responses;

namespace App.Core.Features.PurchaseOrders.GetPurchaseOrders;

/// <summary>
/// 采购订单分页查询请求（Query 参数绑定；只读）
/// </summary>
public sealed class GetPurchaseOrdersRequest : IRequest<PagedResult<PurchaseOrderListItemDto>>
{
    /// <summary>页码，从 1 起</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; init; } = 20;

    /// <summary>订单号 / 供应商名称关键词，可空</summary>
    public string? Keyword { get; init; }

    /// <summary>供应商 id，可空</summary>
    public Guid? PartnerId { get; init; }

    /// <summary>订单流转状态，可空（0 已作废 / 1 待收货 / 2 部分收货 / 3 已完成 / 4 已关闭）</summary>
    public OrderFlowStatus? FlowStatus { get; init; }

    /// <summary>起始下单日期（含），可空（前端传本地当天 00:00 的 UTC ISO 串）</summary>
    public DateTimeOffset? Start { get; init; }

    /// <summary>结束下单日期（含），可空（前端传本地当天 23:59:59 的 UTC ISO 串）</summary>
    public DateTimeOffset? End { get; init; }
}
