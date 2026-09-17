using App.Core.Abstractions;

namespace App.Core.Features.PurchaseOrders.CreatePurchaseOrder;

/// <summary>
/// 新增采购订单请求（计划单据：保存后不动库存、不写库存流水）。
/// 小计 / 总额不在此请求中——后端按 数量 × 单价 重算，不信任前端传值（见 design.md §3.4）。
/// </summary>
public sealed class CreatePurchaseOrderRequest : IRequest<PurchaseOrderDetailDto>
{
    /// <summary>供应商 id</summary>
    public required Guid PartnerId { get; init; }

    /// <summary>下单日期（UTC 午夜，前端所选日期的本地 0 点转 UTC ISO 串）</summary>
    public required DateTimeOffset OrderDate { get; init; }

    /// <summary>预计到货日期，可空（不早于下单日期）</summary>
    public DateTimeOffset? ExpectedDate { get; init; }

    /// <summary>明细行（1–100 行；productId / quantity / unitPrice）</summary>
    public required IReadOnlyList<CreatePurchaseOrderItem> Items { get; init; }

    /// <summary>备注，可空</summary>
    public string? Remark { get; init; }
}

/// <summary>采购订单明细行入参（快照字段由后端从商品档案带出）</summary>
public sealed class CreatePurchaseOrderItem
{
    /// <summary>商品 id</summary>
    public required Guid ProductId { get; init; }

    /// <summary>订购数量（≥ 1）</summary>
    public required int Quantity { get; init; }

    /// <summary>单价（≥ 0，默认带出商品采购价、开单时可改）</summary>
    public required decimal UnitPrice { get; init; }
}
