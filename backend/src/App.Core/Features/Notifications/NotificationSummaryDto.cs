namespace App.Core.Features.Notifications;

/// <summary>
/// 顶栏铃铛未读汇总出参（specs/041-erp-stock-alert/design.md §3.5）：
/// 未读数 + 最近 <c>NotificationFieldConstraints.RecentCount</c> 条消息（按生成时间倒序）。
/// </summary>
public sealed class NotificationSummaryDto
{
    /// <summary>未读消息数</summary>
    public int UnreadCount { get; init; }

    /// <summary>最近消息（至多 RecentCount 条，按生成时间倒序）</summary>
    public IReadOnlyList<NotificationListItemDto> Recent { get; init; } = [];
}
