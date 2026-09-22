namespace App.Core.Features.AuditLogs;

/// <summary>
/// 操作日志列表出参（列表不返回字段级差异：该列体积大且列表页不展示）
/// </summary>
public sealed class AuditLogListItemDto
{
    /// <summary>日志 ID</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>操作人登录名快照；系统动作为空</summary>
    public string? Username { get; init; }

    /// <summary>操作人显示名快照</summary>
    public string? DisplayName { get; init; }

    /// <summary>资源类型枚举值</summary>
    public int Resource { get; init; }

    /// <summary>动作枚举值</summary>
    public int Action { get; init; }

    /// <summary>业务标识快照（单号 / 编码 / 用户名 / 角色名）</summary>
    public string? ResourceNo { get; init; }

    /// <summary>中文摘要</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>操作时间</summary>
    public DateTimeOffset CreatedAt { get; init; }
}
