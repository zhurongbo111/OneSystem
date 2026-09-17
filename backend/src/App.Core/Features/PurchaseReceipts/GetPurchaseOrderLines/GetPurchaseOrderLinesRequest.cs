using App.Core.Abstractions;

namespace App.Core.Features.PurchaseReceipts.GetPurchaseOrderLines;

/// <summary>
/// 关联订单明细查询请求（入库开单页选择订单后带出明细与未收数量；只读）
/// </summary>
public sealed class GetPurchaseOrderLinesRequest : IRequest<PurchaseOrderLinesDto>
{
    /// <summary>采购订单 id</summary>
    public required Guid OrderId { get; init; }
}
