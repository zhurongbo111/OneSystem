using App.Core.Abstractions;

namespace App.Core.Features.Settlements.VoidSettlement;

/// <summary>
/// 作废收付款单请求（PUT /api/settlements/{id}/void；逐行回退被核销单据已结算金额）
/// </summary>
public sealed class VoidSettlementRequest : IRequest<SettlementDetailDto>
{
    /// <summary>收付款单 id（由控制器从路由注入，body 不传）</summary>
    public required Guid Id { get; init; }
}
