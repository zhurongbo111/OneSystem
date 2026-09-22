using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 会计科目树节点读模型（只供用例 ↔ 出参映射传递，不暴露到 API）。
/// 用例取全量科目后内存建树（<see cref="Children"/> 已按同级排序填充），
/// 「末级科目」为派生规则（无子科目即末级），不落列
/// （specs/031-erp-finance-master/design.md §3.1）。
/// </summary>
public sealed record AccountTreeNode
{
    /// <summary>科目 ID</summary>
    public required Guid Id { get; init; }

    /// <summary>科目编码</summary>
    public required string Code { get; init; }

    /// <summary>科目名称</summary>
    public required string Name { get; init; }

    /// <summary>科目类别</summary>
    public required AccountCategory Category { get; init; }

    /// <summary>余额方向</summary>
    public required AccountDirection Direction { get; init; }

    /// <summary>上级科目 id（<c>null</c> 表示一级科目）</summary>
    public required Guid? ParentId { get; init; }

    /// <summary>同级排序</summary>
    public required int SortOrder { get; init; }

    /// <summary>是否预置科目（不可删除）</summary>
    public required bool IsPreset { get; init; }

    /// <summary>科目状态</summary>
    public required AccountStatus Status { get; init; }

    /// <summary>备注，可空</summary>
    public required string? Remark { get; init; }

    /// <summary>子科目（已按同级排序，空集合表示末级科目）</summary>
    public required IReadOnlyList<AccountTreeNode> Children { get; init; }
}