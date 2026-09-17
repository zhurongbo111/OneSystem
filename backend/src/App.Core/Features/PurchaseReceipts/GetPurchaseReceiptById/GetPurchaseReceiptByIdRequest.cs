using App.Core.Abstractions;

namespace App.Core.Features.PurchaseReceipts.GetPurchaseReceiptById;

/// <summary>
/// 采购单详情查询请求（只读；纯参数用例，无格式校验器）
/// </summary>
public sealed class GetPurchaseReceiptByIdRequest : IRequest<PurchaseReceiptDetailDto>
{
    /// <summary>采购单 id</summary>
    public required Guid Id { get; init; }
}
