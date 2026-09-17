using App.Core.Abstractions;

namespace App.Core.Features.PurchaseOrders.ClosePurchaseOrder;

/// <summary>
/// 关闭采购订单请求（人工决定剩余不再收货；保留累计量，不再接受关联入库，design.md §5）
/// </summary>
public sealed class ClosePurchaseOrderRequest : IRequest<PurchaseOrderDetailDto>
{
    /// <summary>订单 id</summary>
    public required Guid Id { get; init; }
}
