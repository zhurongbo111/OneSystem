using App.Core.Abstractions;

namespace App.Core.Features.SalesShipments.GetSalesOrderLines;

/// <summary>
/// 关联订单明细查询请求（出库开单页选择订单后带出明细与未发数量；只读）
/// </summary>
public sealed class GetSalesOrderLinesRequest : IRequest<SalesOrderLinesDto>
{
    /// <summary>销售订单 id</summary>
    public required Guid OrderId { get; init; }
}
