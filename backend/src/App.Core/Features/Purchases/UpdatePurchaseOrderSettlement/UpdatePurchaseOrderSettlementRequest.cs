using App.Core.Abstractions;

namespace App.Core.Features.Purchases.UpdatePurchaseOrderSettlement;

/// <summary>
/// 采购单结算状态更新请求（PUT /api/purchase-orders/{id}/settlement；仅 未付 ↔ 已付；库存不变）
/// </summary>
public sealed class UpdatePurchaseOrderSettlementRequest : IRequest<PurchaseOrderDetailDto>
{
    /// <summary>采购单 id（由控制器从路由注入，body 不传）</summary>
    public Guid Id { get; init; }

    /// <summary>目标结算状态（0 未付 / 1 已付）</summary>
    public int SettlementStatus { get; init; }
}
