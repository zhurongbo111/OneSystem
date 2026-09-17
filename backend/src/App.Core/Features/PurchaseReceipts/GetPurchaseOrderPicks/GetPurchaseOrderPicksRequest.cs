using App.Core.Abstractions;

namespace App.Core.Features.PurchaseReceipts.GetPurchaseOrderPicks;

/// <summary>
/// 可关联采购订单候选查询请求（入库开单页「关联订单」下拉；只读）
/// </summary>
public sealed class GetPurchaseOrderPicksRequest : IRequest<IReadOnlyList<PurchaseOrderPickDto>>
{
    /// <summary>供应商 id（候选订单按该供应商过滤）</summary>
    public required Guid PartnerId { get; init; }
}
