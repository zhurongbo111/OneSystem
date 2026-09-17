namespace App.Core.Features.SalesShipments;

/// <summary>
/// 可关联销售订单候选出参（出库开单页「关联订单」下拉：指定客户且状态为待发货 / 部分发货）
/// </summary>
public sealed class SalesOrderPickDto
{
    /// <summary>订单 id</summary>
    public required string Id { get; init; }

    /// <summary>订单号</summary>
    public required string OrderNo { get; init; }

    /// <summary>下单日期</summary>
    public required DateTimeOffset OrderDate { get; init; }

    /// <summary>预计发货日期，可空</summary>
    public DateTimeOffset? ExpectedDate { get; init; }

    /// <summary>订单总金额</summary>
    public required decimal TotalAmount { get; init; }
}

/// <summary>
/// 关联订单明细出参（出库开单页带出：订单头 + 明细含未发数量）
/// </summary>
public sealed class SalesOrderLinesDto
{
    /// <summary>订单 id</summary>
    public required string OrderId { get; init; }

    /// <summary>订单号</summary>
    public required string OrderNo { get; init; }

    /// <summary>客户 id</summary>
    public required string PartnerId { get; init; }

    /// <summary>客户名称快照</summary>
    public required string PartnerName { get; init; }

    /// <summary>预计发货日期，可空</summary>
    public DateTimeOffset? ExpectedDate { get; init; }

    /// <summary>订单明细行（含未发数量，按插入顺序）</summary>
    public required IReadOnlyList<SalesOrderLineDto> Items { get; init; }
}

/// <summary>关联订单明细行出参（未发数量 = Quantity − FulfilledQuantity，推导值）</summary>
public sealed class SalesOrderLineDto
{
    /// <summary>订单明细行 id（提交出库单时作为明细行的 orderItemId）</summary>
    public required string OrderItemId { get; init; }

    /// <summary>商品 id</summary>
    public required string ProductId { get; init; }

    /// <summary>商品名称快照</summary>
    public required string ProductName { get; init; }

    /// <summary>计量单位快照</summary>
    public required string Unit { get; init; }

    /// <summary>订购数量</summary>
    public required int Quantity { get; init; }

    /// <summary>累计已发数量</summary>
    public required int FulfilledQuantity { get; init; }

    /// <summary>未发数量（= 订购数量 − 累计已发）</summary>
    public required int RemainingQuantity { get; init; }

    /// <summary>订单单价快照（本次出库单价只读取此值）</summary>
    public required decimal UnitPrice { get; init; }
}
