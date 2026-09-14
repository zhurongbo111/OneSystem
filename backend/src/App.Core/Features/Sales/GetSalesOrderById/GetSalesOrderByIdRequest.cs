using App.Core.Abstractions;
using App.Core.Features.Sales;

namespace App.Core.Features.Sales.GetSalesOrderById;

/// <summary>
/// 销售单详情查询请求（只读；纯参数用例，无格式校验器）
/// </summary>
public sealed class GetSalesOrderByIdRequest : IRequest<SalesOrderDetailDto>
{
    /// <summary>销售单 id</summary>
    public required Guid Id { get; init; }
}
