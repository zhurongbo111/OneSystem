using App.Core.Abstractions;

namespace App.Core.Features.SalesReturns.VoidSalesReturn;

/// <summary>
/// 销售退货单作废请求（仅改状态，不删数据；回冲库存，见 design.md §3.5）
/// </summary>
public sealed class VoidSalesReturnRequest : IRequest<SalesReturnDetailDto>
{
    /// <summary>销售退货单 id</summary>
    public required Guid Id { get; init; }
}
