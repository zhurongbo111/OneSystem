namespace App.Core.Features.Notifications;

/// <summary>
/// 全部标记已读出参（specs/041-erp-stock-alert/design.md §3.5）：本次标记条数。
/// </summary>
public sealed class MarkAllNotificationsReadResponse
{
    /// <summary>本次标记为已读的条数</summary>
    public int AffectedCount { get; init; }
}
