namespace App.Core.Entities;

/// <summary>
/// 站内信实体（对应 PostgreSQL 表 Notifications，specs/041-erp-stock-alert/design.md §2.1）。
/// 纯追加 + 只标记已读：无删除、无编辑入口（通知历史有追溯价值）；
/// 跳转目标以「路由名 + query JSON」落地，供前端 <c>router.push({ name, query })</c> 直接使用。
/// </summary>
public sealed class Notification
{
    /// <summary>站内信 ID</summary>
    public Guid Id { get; set; }

    /// <summary>接收人用户 ID（外键 → Users(Id)；只查本人消息）</summary>
    public Guid UserId { get; set; }

    /// <summary>站内信类型（见 <see cref="NotificationType"/>）</summary>
    public NotificationType Type { get; set; }

    /// <summary>标题（固定模板文案）</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>内容（固定模板文案，含业务标识便于人读）</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>跳转路由名（可空；为空表示仅提示、不跳转）</summary>
    public string? LinkRouteName { get; set; }

    /// <summary>跳转 query（JSON 文本，可空；与 <see cref="LinkRouteName"/> 配对使用）</summary>
    public string? LinkQuery { get; set; }

    /// <summary>业务对象标识（低库存 <c>product:&lt;id&gt;:warehouse:&lt;id&gt;</c> / 批次 <c>batch:&lt;id&gt;:warehouse:&lt;id&gt;</c>，供排查）</summary>
    public string? ResourceKey { get; set; }

    /// <summary>已读时间（为空 = 未读）</summary>
    public DateTimeOffset? ReadAt { get; set; }

    /// <summary>生成时间</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
