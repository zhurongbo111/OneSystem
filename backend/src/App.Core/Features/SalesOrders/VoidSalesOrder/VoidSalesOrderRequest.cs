using App.Core.Abstractions;

namespace App.Core.Features.SalesOrders.VoidSalesOrder;

/// <summary>
/// 销售订单作废请求（仅改流转状态，不删数据；仅「待发货」状态可作废，design.md §3.4）
/// </summary>
public sealed class VoidSalesOrderRequest : IRequest<SalesOrderDetailDto>
{
    /// <summary>订单 id</summary>
    public required Guid Id { get; init; }
}
