using App.Core.Abstractions;

namespace App.Core.Features.Sales.UpdateSalesOrderSettlement;

/// <summary>
/// 销售单结算状态更新请求（PUT /api/sales-orders/{id}/settlement；仅 未收 ↔ 已收；库存不变）
/// </summary>
public sealed class UpdateSalesOrderSettlementRequest : IRequest<SalesOrderDetailDto>
{
    /// <summary>销售单 id（由控制器从路由注入，body 不传）</summary>
    public Guid Id { get; init; }

    /// <summary>目标结算状态（0 未收 / 1 已收）</summary>
    public int SettlementStatus { get; init; }
}
