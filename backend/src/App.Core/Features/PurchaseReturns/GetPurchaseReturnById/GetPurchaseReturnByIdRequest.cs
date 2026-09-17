using App.Core.Abstractions;

namespace App.Core.Features.PurchaseReturns.GetPurchaseReturnById;

/// <summary>
/// 采购退货单详情查询请求（只读；纯参数用例，无格式校验器）
/// </summary>
public sealed class GetPurchaseReturnByIdRequest : IRequest<PurchaseReturnDetailDto>
{
    /// <summary>采购退货单 id</summary>
    public required Guid Id { get; init; }
}
