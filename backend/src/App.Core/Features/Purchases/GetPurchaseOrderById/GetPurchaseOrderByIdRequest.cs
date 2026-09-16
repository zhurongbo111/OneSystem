using App.Core.Abstractions;

namespace App.Core.Features.Purchases.GetPurchaseOrderById;

/// <summary>
/// 采购单详情查询请求（只读；纯参数用例，无格式校验器）
/// </summary>
public sealed class GetPurchaseOrderByIdRequest : IRequest<PurchaseOrderDetailDto>
{
    /// <summary>采购单 id</summary>
    public required Guid Id { get; init; }
}
