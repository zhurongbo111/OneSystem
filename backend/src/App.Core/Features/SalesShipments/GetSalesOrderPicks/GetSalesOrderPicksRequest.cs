using App.Core.Abstractions;

namespace App.Core.Features.SalesShipments.GetSalesOrderPicks;

/// <summary>
/// 可关联销售订单候选查询请求（出库开单页「关联订单」下拉；只读）
/// </summary>
public sealed class GetSalesOrderPicksRequest : IRequest<IReadOnlyList<SalesOrderPickDto>>
{
    /// <summary>客户 id（候选订单按该客户过滤）</summary>
    public required Guid PartnerId { get; init; }
}
