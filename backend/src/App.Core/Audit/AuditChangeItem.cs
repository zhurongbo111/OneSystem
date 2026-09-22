namespace App.Core.Audit;

/// <summary>
/// 审计日志的字段级差异项：落库为 <c>jsonb</c> 数组的一项，出参经 <c>AuditLogsDtoMapper</c> 反序列化后返回前端。
/// </summary>
public sealed class AuditChangeItem
{
    /// <summary>实体字段名（camelCase）</summary>
    public string Field { get; init; } = string.Empty;

    /// <summary>中文字段名（取该域页面既有中文文案）</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>变更前的值文本；新增类动作为空</summary>
    public string? Before { get; init; }

    /// <summary>变更后的值文本；删除 / 清空类动作为空</summary>
    public string? After { get; init; }
}
