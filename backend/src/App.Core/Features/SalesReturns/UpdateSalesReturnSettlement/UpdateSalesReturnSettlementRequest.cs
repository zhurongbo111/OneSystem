using App.Core.Abstractions;

namespace App.Core.Features.SalesReturns.UpdateSalesReturnSettlement;

/// <summary>
/// 销售退货单结算状态更新请求（PUT /api/sales-returns/{id}/settlement；仅 未结算 ↔ 已结算；库存不变）
/// </summary>
public sealed class UpdateSalesReturnSettlementRequest : IRequest<SalesReturnDetailDto>
{
    /// <summary>销售退货单 id（由控制器从路由注入，body 不传）</summary>
    public Guid Id { get; init; }

    /// <summary>目标结算状态（0 未结算 / 1 已结算）</summary>
    public int SettlementStatus { get; init; }
}
