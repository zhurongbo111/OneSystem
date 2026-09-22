namespace App.Core.Features.Accounts;

/// <summary>
/// 会计科目树节点出参模型（科目树列表页数据源；子节点已按同级排序）。
/// 枚举统一以**整型**输出（<c>AccountCategory</c> / <c>AccountDirection</c> / <c>AccountStatus</c>），前端按整型渲染。
/// 「末级科目」为派生规则（<see cref="Children"/> 为空即末级，可记账），不落列。
/// </summary>
public sealed class AccountTreeNodeDto
{
    /// <summary>科目 ID</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>科目编码</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>科目名称</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>科目类别（1 资产 / 2 负债 / 3 权益 / 4 成本 / 5 损益）</summary>
    public int Category { get; init; }

    /// <summary>余额方向（1 借 / 2 贷）</summary>
    public int Direction { get; init; }

    /// <summary>上级科目 id（<c>null</c> 表示一级科目）</summary>
    public string? ParentId { get; init; }

    /// <summary>同级排序</summary>
    public int SortOrder { get; init; }

    /// <summary>是否预置科目（预置科目不可删除）</summary>
    public bool IsPreset { get; init; }

    /// <summary>科目状态（0 停用 / 1 启用）</summary>
    public int Status { get; init; }

    /// <summary>备注</summary>
    public string? Remark { get; init; }

    /// <summary>子科目（已按同级排序，空集合表示末级科目）</summary>
    public IReadOnlyList<AccountTreeNodeDto> Children { get; init; } = [];
}