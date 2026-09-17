using App.Core.Abstractions;

namespace App.Core.Features.SalesReturns.GetSalesReturnById;

/// <summary>
/// 销售退货单详情查询请求（只读；纯参数用例，无格式校验器）
/// </summary>
public sealed class GetSalesReturnByIdRequest : IRequest<SalesReturnDetailDto>
{
    /// <summary>销售退货单 id</summary>
    public required Guid Id { get; init; }
}
