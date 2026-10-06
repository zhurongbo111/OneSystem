using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Opportunities.UpdateOpportunityStage;

/// <summary>
/// 商机阶段推进请求（design.md §3.3 / §3.4）：终态（赢单 / 输单）后不可再改阶段（40169）。
/// </summary>
public sealed class UpdateOpportunityStageRequest : IRequest<OpportunityDetailDto>
{
    /// <summary>商机 id（由路由覆盖写入）</summary>
    public Guid Id { get; init; }

    /// <summary>目标阶段</summary>
    public OpportunityStage Stage { get; init; } = OpportunityStage.Initial;
}
