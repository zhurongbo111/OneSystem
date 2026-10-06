using App.Core.Abstractions;

namespace App.Core.Features.Activities.GetOpportunityActivities;

/// <summary>
/// 商机跟进活动查询请求（只读；design.md §3.3 按域拆分端点，避免动态权限）
/// </summary>
public sealed class GetOpportunityActivitiesRequest : IRequest<IReadOnlyList<ActivityItemDto>>
{
    /// <summary>商机 id</summary>
    public required Guid OpportunityId { get; init; }
}
