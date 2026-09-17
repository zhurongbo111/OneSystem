using App.Core.Abstractions;

namespace App.Core.Features.PurchaseReturns.UpdatePurchaseReturnSettlement;

/// <summary>
/// 采购退货单结算状态更新请求（PUT /api/purchase-returns/{id}/settlement；仅 未结算 ↔ 已结算；库存不变）
/// </summary>
public sealed class UpdatePurchaseReturnSettlementRequest : IRequest<PurchaseReturnDetailDto>
{
    /// <summary>采购退货单 id（由控制器从路由注入，body 不传）</summary>
    public Guid Id { get; init; }

    /// <summary>目标结算状态（0 未结算 / 1 已结算）</summary>
    public int SettlementStatus { get; init; }
}
