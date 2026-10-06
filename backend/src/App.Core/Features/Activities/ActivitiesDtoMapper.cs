using App.Core.Abstractions;

namespace App.Core.Features.Activities;

/// <summary>
/// 跟进活动读模型 → 出参映射（禁止把实体暴露到 API；派生字段在 Mapper 内计算）。
/// </summary>
internal static class ActivitiesDtoMapper
{
    /// <summary>
    /// 活动读模型（实体 + 记录人显示名）转活动 DTO
    /// </summary>
    public static ActivityItemDto ToActivityItemDto(ActivityItem item)
        => new()
        {
            Id = item.Activity.Id.ToString(),
            BizType = (int)item.Activity.BizType,
            BizId = item.Activity.BizId.ToString(),
            Type = (int)item.Activity.Type,
            Content = item.Activity.Content,
            ActivityTime = item.Activity.ActivityTime,
            RecorderName = item.RecorderName,
            CreatedAt = item.Activity.CreatedAt,
        };
}
