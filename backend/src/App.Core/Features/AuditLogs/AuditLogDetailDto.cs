namespace App.Core.Features.AuditLogs;

/// <summary>
/// 操作日志详情出参（在列表项基础上追加业务对象 id 与字段级差异）
/// </summary>
public sealed class AuditLogDetailDto
{
    /// <summary>日志 ID</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>操作人 id；系统动作为空</summary>
    public string? UserId { get; init; }

    /// <summary>操作人登录名快照</summary>
    public string? Username { get; init; }

    /// <summary>操作人显示名快照</summary>
    public string? DisplayName { get; init; }

    /// <summary>资源类型枚举值</summary>
    public int Resource { get; init; }

    /// <summary>动作枚举值</summary>
    public int Action { get; init; }

    /// <summary>业务对象 id（如单据 id / 商品 id）</summary>
    public string? ResourceId { get; init; }

    /// <summary>业务标识快照</summary>
    public string? ResourceNo { get; init; }

    /// <summary>中文摘要</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>操作时间</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>字段级差异（无差异时为空数组）</summary>
    public IReadOnlyList<AuditChangeDto> Changes { get; init; } = [];
}

/// <summary>
/// 字段级差异出参：<c>before</c> / <c>after</c> 为空表示该侧无值（新增 / 清空）
/// </summary>
public sealed class AuditChangeDto
{
    /// <summary>实体字段名（camelCase）</summary>
    public string Field { get; init; } = string.Empty;

    /// <summary>中文字段名</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>变更前的值文本；新增类动作为空</summary>
    public string? Before { get; init; }

    /// <summary>变更后的值文本；清空类动作为空</summary>
    public string? After { get; init; }
}
