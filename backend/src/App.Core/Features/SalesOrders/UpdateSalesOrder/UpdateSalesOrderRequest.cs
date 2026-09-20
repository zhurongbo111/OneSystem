using App.Core.Abstractions;

namespace App.Core.Features.SalesOrders.UpdateSalesOrder;

/// <summary>
/// 编辑销售订单请求（仅「待发货」状态可改；明细整体替换）。
/// 订单号不可改（请求体不含），小计 / 总额由后端重算（design.md §3.4）。
/// </summary>
public sealed class UpdateSalesOrderRequest : IRequest<SalesOrderDetailDto>
{
    /// <summary>订单 id（取自路由参数；请求体不含，缺失时反序列化为空值，控制器以路由 id 覆盖）</summary>
    public Guid Id { get; init; }

    /// <summary>客户 id</summary>
    public required Guid PartnerId { get; init; }

    /// <summary>下单日期（UTC 午夜）</summary>
    public required DateTimeOffset OrderDate { get; init; }

    /// <summary>预计发货日期，可空（不早于下单日期）</summary>
    public DateTimeOffset? ExpectedDate { get; init; }

    /// <summary>明细行（1–100 行；整体替换）</summary>
    public required IReadOnlyList<UpdateSalesOrderItem> Items { get; init; }

    /// <summary>备注，可空</summary>
    public string? Remark { get; init; }
}

/// <summary>销售订单明细行入参（编辑时整体替换，累计已发数量恒为 0）</summary>
public sealed class UpdateSalesOrderItem
{
    /// <summary>商品 id</summary>
    public required Guid ProductId { get; init; }

    /// <summary>订购数量（≥ 1）</summary>
    public required int Quantity { get; init; }

    /// <summary>单价（≥ 0）</summary>
    public required decimal UnitPrice { get; init; }
}
