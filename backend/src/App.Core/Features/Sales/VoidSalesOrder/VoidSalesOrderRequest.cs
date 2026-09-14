using App.Core.Abstractions;

namespace App.Core.Features.Sales.VoidSalesOrder;

/// <summary>
/// 销售单作废请求（仅改状态，不删数据；回冲库存，见 design.md §3.4）
/// </summary>
public sealed class VoidSalesOrderRequest : IRequest<SalesOrderDetailDto>
{
    /// <summary>销售单 id</summary>
    public required Guid Id { get; init; }
}
