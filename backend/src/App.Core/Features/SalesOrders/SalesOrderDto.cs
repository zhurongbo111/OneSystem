namespace App.Core.Features.SalesOrders;

/// <summary>
/// 销售订单出参共享模型（主表 + 明细，与后端 DTO camelCase 一一对应；列表行用 SalesOrderListItemDto）。
/// 状态文案与颜色映射见 specs/024-erp-order-flow design.md §0（销售侧为待发货 / 部分发货）。
/// </summary>
public sealed class SalesOrderDetailDto
{
    /// <summary>订单 id</summary>
    public required string Id { get; init; }

    /// <summary>订单号</summary>
    public required string OrderNo { get; init; }

    /// <summary>客户 id</summary>
    public required string PartnerId { get; init; }

    /// <summary>客户名称快照</summary>
    public required string PartnerName { get; init; }

    /// <summary>下单日期</summary>
    public required DateTimeOffset OrderDate { get; init; }

    /// <summary>预计发货日期，可空</summary>
    public DateTimeOffset? ExpectedDate { get; init; }

    /// <summary>总金额（后端重算值）</summary>
    public required decimal TotalAmount { get; init; }

    /// <summary>订单流转状态（0 已作废 / 1 待发货 / 2 部分发货 / 3 已完成 / 4 已关闭）</summary>
    public required int FlowStatus { get; init; }

    /// <summary>备注</summary>
    public string? Remark { get; init; }

    /// <summary>创建人用户 id</summary>
    public string? CreatedBy { get; init; }

    /// <summary>创建时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>明细行（按插入顺序）</summary>
    public required IReadOnlyList<SalesOrderItemDto> Items { get; init; }
}

/// <summary>销售订单明细出参模型（含累计已发与未发数量）</summary>
public sealed class SalesOrderItemDto
{
    /// <summary>明细行 id</summary>
    public required string Id { get; init; }

    /// <summary>商品 id</summary>
    public required string ProductId { get; init; }

    /// <summary>商品名称快照</summary>
    public required string ProductName { get; init; }

    /// <summary>计量单位快照</summary>
    public required string Unit { get; init; }

    /// <summary>订购数量</summary>
    public required int Quantity { get; init; }

    /// <summary>累计已发数量（由出库单关联回写 / 作废回退）</summary>
    public required int FulfilledQuantity { get; init; }

    /// <summary>未发数量 = 订购数量 − 累计已发（推导值，不落列）</summary>
    public required int RemainingQuantity { get; init; }

    /// <summary>单价快照</summary>
    public required decimal UnitPrice { get; init; }

    /// <summary>小计（后端重算值）</summary>
    public required decimal Subtotal { get; init; }
}

/// <summary>销售订单列表行出参模型（含未发数量合计，供在途跟单）</summary>
public sealed class SalesOrderListItemDto
{
    /// <summary>订单 id</summary>
    public required string Id { get; init; }

    /// <summary>订单号</summary>
    public required string OrderNo { get; init; }

    /// <summary>客户 id</summary>
    public required string PartnerId { get; init; }

    /// <summary>客户名称快照</summary>
    public required string PartnerName { get; init; }

    /// <summary>下单日期</summary>
    public required DateTimeOffset OrderDate { get; init; }

    /// <summary>预计发货日期，可空</summary>
    public DateTimeOffset? ExpectedDate { get; init; }

    /// <summary>总金额</summary>
    public required decimal TotalAmount { get; init; }

    /// <summary>未发数量合计（Σ 明细未执行量；= 0 表示已发齐）</summary>
    public required int UnfulfilledQuantity { get; init; }

    /// <summary>订单流转状态（0 已作废 / 1 待发货 / 2 部分发货 / 3 已完成 / 4 已关闭）</summary>
    public required int FlowStatus { get; init; }

    /// <summary>创建时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
