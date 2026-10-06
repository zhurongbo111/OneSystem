using App.Core.Abstractions;

namespace App.Core.Features.Activities.GetLeadActivities;

/// <summary>
/// 线索跟进活动查询请求（只读；design.md §3.3 按域拆分端点，避免动态权限）
/// </summary>
public sealed class GetLeadActivitiesRequest : IRequest<IReadOnlyList<ActivityItemDto>>
{
    /// <summary>线索 id</summary>
    public required Guid LeadId { get; init; }
}
