namespace App.Core.Features.Notifications;

/// <summary>
/// 站内信列表行 / 顶栏最近消息出参（specs/041-erp-stock-alert/design.md §3.5）。
/// 类型以整型输出（0 低库存 / 1 近效期 / 2 过期），文案与颜色由前端 <c>api/notification.ts</c> 映射。
/// </summary>
public sealed class NotificationListItemDto
{
    /// <summary>站内信 ID</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>消息类型（0 低库存 / 1 近效期 / 2 过期）</summary>
    public int Type { get; init; }

    /// <summary>标题</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>内容</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>跳转路由名（可空）</summary>
    public string? LinkRouteName { get; init; }

    /// <summary>跳转 query（JSON 文本，可空）</summary>
    public string? LinkQuery { get; init; }

    /// <summary>业务对象标识（可空，供排查）</summary>
    public string? ResourceKey { get; init; }

    /// <summary>是否已读</summary>
    public bool IsRead { get; init; }

    /// <summary>已读时间（为空 = 未读）</summary>
    public DateTimeOffset? ReadAt { get; init; }

    /// <summary>生成时间</summary>
    public DateTimeOffset CreatedAt { get; init; }
}
