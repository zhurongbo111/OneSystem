using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 跟进活动读模型（仓储出参契约 ↔ Mapper 传递，不暴露到 API）：
/// 活动实体 + 记录人显示名（联查 <see cref="Activity.CreatedBy"/> 对应用户，实体上没有该字段，故独立成读模型，后端规则 §4.3）。
/// </summary>
public sealed record ActivityItem
{
    /// <summary>活动实体</summary>
    public required Activity Activity { get; init; }

    /// <summary>记录人显示名，取不到时为「系统」占位（Mapper 内补）</summary>
    public string? RecorderName { get; init; }
}
