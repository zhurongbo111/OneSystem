using App.Core.Abstractions;

namespace App.Core.Features.PurchaseReceipts.VoidPurchaseReceipt;

/// <summary>
/// 采购单作废请求（仅改状态，不删数据；回冲库存，见 design.md §3.4）
/// </summary>
public sealed class VoidPurchaseReceiptRequest : IRequest<PurchaseReceiptDetailDto>
{
    /// <summary>采购单 id</summary>
    public required Guid Id { get; init; }
}
