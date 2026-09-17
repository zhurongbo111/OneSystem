using App.Core.Abstractions;

namespace App.Core.Features.SalesOrders.CloseSalesOrder;

/// <summary>
/// 关闭销售订单请求（人工决定剩余不再发货；保留累计量，不再接受关联出库，design.md §5）
/// </summary>
public sealed class CloseSalesOrderRequest : IRequest<SalesOrderDetailDto>
{
    /// <summary>订单 id</summary>
    public required Guid Id { get; init; }
}
