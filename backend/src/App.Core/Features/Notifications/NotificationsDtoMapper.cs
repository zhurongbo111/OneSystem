using App.Core.Entities;

namespace App.Core.Features.Notifications;

/// <summary>
/// 站内信实体 → 出参模型 的映射（集中一处，避免各用例重复拼装；后端规则 §4.3）。
/// </summary>
internal static class NotificationsDtoMapper
{
    /// <summary>映射列表行 / 最近消息出参（已读标记由 ReadAt 派生）</summary>
    public static NotificationListItemDto ToListItemDto(Notification notification)
        => new()
        {
            Id = notification.Id.ToString(),
            Type = (int)notification.Type,
            Title = notification.Title,
            Content = notification.Content,
            LinkRouteName = notification.LinkRouteName,
            LinkQuery = notification.LinkQuery,
            ResourceKey = notification.ResourceKey,
            IsRead = notification.ReadAt is not null,
            ReadAt = notification.ReadAt,
            CreatedAt = notification.CreatedAt,
        };
}
