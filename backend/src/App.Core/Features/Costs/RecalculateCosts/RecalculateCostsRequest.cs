using App.Core.Abstractions;

namespace App.Core.Features.Costs.RecalculateCosts;

/// <summary>
/// 成本重算请求（erp-cost design §3.3）：三个筛选条件均可空，不传表示全量重算。
/// 期间只决定「写回哪些流水」——结存推演始终从最早流水开始，保证结存金额与均价正确。
/// </summary>
public sealed class RecalculateCostsRequest : IRequest<CostRecalculateResultDto>
{
    /// <summary>商品 id，可空（不传表示全部商品）</summary>
    public Guid? ProductId { get; init; }

    /// <summary>期间起（含），可空</summary>
    public DateTimeOffset? Start { get; init; }

    /// <summary>期间止（不含），可空</summary>
    public DateTimeOffset? End { get; init; }
}
