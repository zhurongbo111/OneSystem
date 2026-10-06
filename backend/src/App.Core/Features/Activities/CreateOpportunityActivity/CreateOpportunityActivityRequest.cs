using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Activities.CreateOpportunityActivity;

/// <summary>
/// 新增商机跟进活动请求（活动只增不改不删：跟进留痕，design.md §0.1）
/// </summary>
public sealed class CreateOpportunityActivityRequest : IRequest<ActivityItemDto>
{
    /// <summary>商机 id（由路由覆盖写入）</summary>
    public Guid OpportunityId { get; init; }

    /// <summary>跟进方式（电话 / 拜访 / 邮件 / 其他）</summary>
    public ActivityType Type { get; init; } = ActivityType.Call;

    /// <summary>跟进内容（必填，1–200）</summary>
    public required string Content { get; init; }

    /// <summary>跟进时间（必填）</summary>
    public DateTimeOffset ActivityTime { get; init; }
}
