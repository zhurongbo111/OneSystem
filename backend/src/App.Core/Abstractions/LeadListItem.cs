using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 线索列表行读模型（仓储出参契约 ↔ Mapper 传递，不暴露到 API）：
/// 线索实体 + 负责人姓名（联查 <see cref="Employee"/>，实体上没有该字段，故独立成读模型，后端规则 §4.3）。
/// </summary>
public sealed record LeadListItem
{
    /// <summary>线索实体</summary>
    public required Lead Lead { get; init; }

    /// <summary>负责人姓名，未指派时为空</summary>
    public string? OwnerName { get; init; }
}
