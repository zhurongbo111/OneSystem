using App.Core.Abstractions;

namespace App.Core.Features.PurchaseReceipts.CreatePurchaseReceipt;

/// <summary>
/// 新增采购入库单请求（一步式：保存即生效，库存立即增加）。
/// 可**可选关联采购订单**（specs/024-erp-order-flow design.md §3.4）：关联时按订单明细收货、
/// 校验不超未收数量并回写订单累计已收；不关联时沿用「货到即入账」直通用法。
/// 小计 / 总额不在此请求中——后端按 数量 × 单价 重算，不信任前端传值（见 design.md §1）。
/// </summary>
public sealed class CreatePurchaseReceiptRequest : IRequest<PurchaseReceiptDetailDto>
{
    /// <summary>供应商 id</summary>
    public required Guid PartnerId { get; init; }

    /// <summary>业务日期（UTC 午夜，前端所选日期的本地 0 点转 UTC ISO 串）</summary>
    public required DateTimeOffset OrderDate { get; init; }

    /// <summary>入库仓 id，可空（038；不传 = 默认仓，兼容存量调用方）</summary>
    public Guid? WarehouseId { get; init; }

    /// <summary>关联采购订单 id，可空（不关联即一步式直通用法；关联时必须与订单供应商一致）</summary>
    public Guid? OrderId { get; init; }

    /// <summary>明细行（1–100 行；productId / quantity / unitPrice）</summary>
    public required IReadOnlyList<CreatePurchaseReceiptItem> Items { get; init; }

    /// <summary>备注，可空</summary>
    public string? Remark { get; init; }
}

/// <summary>采购入库单明细行入参（快照字段由后端从商品档案带出）</summary>
public sealed class CreatePurchaseReceiptItem
{
    /// <summary>商品 id</summary>
    public required Guid ProductId { get; init; }

    /// <summary>数量（≥ 1）</summary>
    public required int Quantity { get; init; }

    /// <summary>单价（≥ 0，默认带出商品采购价、开单时可改）</summary>
    public required decimal UnitPrice { get; init; }

    /// <summary>关联采购订单明细行 id，可空（关联订单时必填，指向本次收货对应的订单行）</summary>
    public Guid? OrderItemId { get; init; }
}
