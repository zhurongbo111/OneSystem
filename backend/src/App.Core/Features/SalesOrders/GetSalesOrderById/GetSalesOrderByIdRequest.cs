using App.Core.Abstractions;

namespace App.Core.Features.SalesOrders.GetSalesOrderById;

/// <summary>
/// 销售订单详情查询请求（只读；纯参数用例，无格式校验器）
/// </summary>
public sealed class GetSalesOrderByIdRequest : IRequest<SalesOrderDetailDto>
{
    /// <summary>订单 id</summary>
    public required Guid Id { get; init; }
}
