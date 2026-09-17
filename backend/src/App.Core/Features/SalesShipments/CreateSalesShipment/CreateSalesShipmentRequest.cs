using App.Core.Abstractions;

namespace App.Core.Features.SalesShipments.CreateSalesShipment;

/// <summary>
/// 新增销售出库单请求（一步式：保存即生效，库存立即减少）。
/// 可**可选关联销售订单**（specs/024-erp-order-flow design.md §3.4）：关联时按订单明细发货、
/// 校验不超未发数量并回写订单累计已发；不关联时沿用「货到即入账」直通用法。
/// 小计 / 总额不在此请求中——后端按 数量 × 单价 重算，不信任前端传值（见 design.md §1）。
/// </summary>
public sealed class CreateSalesShipmentRequest : IRequest<SalesShipmentDetailDto>
{
    /// <summary>客户 id</summary>
    public required Guid PartnerId { get; init; }

    /// <summary>业务日期（UTC 午夜，前端所选日期的本地 0 点转 UTC ISO 串）</summary>
    public required DateTimeOffset OrderDate { get; init; }

    /// <summary>关联销售订单 id，可空（不关联即一步式直通用法；关联时必须与订单客户一致）</summary>
    public Guid? OrderId { get; init; }

    /// <summary>明细行（1–100 行；productId / quantity / unitPrice）</summary>
    public required IReadOnlyList<CreateSalesShipmentItem> Items { get; init; }

    /// <summary>备注，可空</summary>
    public string? Remark { get; init; }
}

/// <summary>销售出库单明细行入参（快照字段由后端从商品档案带出）</summary>
public sealed class CreateSalesShipmentItem
{
    /// <summary>商品 id</summary>
    public required Guid ProductId { get; init; }

    /// <summary>数量（≥ 1）</summary>
    public required int Quantity { get; init; }

    /// <summary>单价（≥ 0，默认带出商品销售价、开单时可改）</summary>
    public required decimal UnitPrice { get; init; }

    /// <summary>关联销售订单明细行 id，可空（关联订单时必填，指向本次发货对应的订单行）</summary>
    public Guid? OrderItemId { get; init; }
}
