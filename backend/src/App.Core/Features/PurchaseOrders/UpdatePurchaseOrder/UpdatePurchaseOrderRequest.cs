using App.Core.Abstractions;

namespace App.Core.Features.PurchaseOrders.UpdatePurchaseOrder;

/// <summary>
/// 编辑采购订单请求（仅「待收货」状态可改；明细整体替换）。
/// 订单号不可改（请求体不含），小计 / 总额由后端重算（design.md §3.4）。
/// </summary>
public sealed class UpdatePurchaseOrderRequest : IRequest<PurchaseOrderDetailDto>
{
    /// <summary>订单 id（取自路由参数）</summary>
    public required Guid Id { get; init; }

    /// <summary>供应商 id</summary>
    public required Guid PartnerId { get; init; }

    /// <summary>下单日期（UTC 午夜）</summary>
    public required DateTimeOffset OrderDate { get; init; }

    /// <summary>预计到货日期，可空（不早于下单日期）</summary>
    public DateTimeOffset? ExpectedDate { get; init; }

    /// <summary>明细行（1–100 行；整体替换）</summary>
    public required IReadOnlyList<UpdatePurchaseOrderItem> Items { get; init; }

    /// <summary>备注，可空</summary>
    public string? Remark { get; init; }
}

/// <summary>采购订单明细行入参（编辑时整体替换，累计已收数量恒为 0）</summary>
public sealed class UpdatePurchaseOrderItem
{
    /// <summary>商品 id</summary>
    public required Guid ProductId { get; init; }

    /// <summary>订购数量（≥ 1）</summary>
    public required int Quantity { get; init; }

    /// <summary>单价（≥ 0）</summary>
    public required decimal UnitPrice { get; init; }
}
