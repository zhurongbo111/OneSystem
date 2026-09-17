using App.Core.Abstractions;

namespace App.Core.Features.PurchaseOrders.VoidPurchaseOrder;

/// <summary>
/// 采购订单作废请求（仅改流转状态，不删数据；仅「待收货」状态可作废，design.md §3.4）
/// </summary>
public sealed class VoidPurchaseOrderRequest : IRequest<PurchaseOrderDetailDto>
{
    /// <summary>订单 id</summary>
    public required Guid Id { get; init; }
}
