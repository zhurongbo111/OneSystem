using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 资产负债表行读模型（仓储出参契约，按**一级科目**列示）。
/// 金额为期末余额（按余额方向取正），见 specs/033-erp-general-ledger/design.md §0.4。
/// </summary>
public sealed record BalanceSheetItem
{
    /// <summary>一级科目 id</summary>
    public required Guid AccountId { get; init; }

    /// <summary>一级科目编码</summary>
    public required string Code { get; init; }

    /// <summary>一级科目名称</summary>
    public required string Name { get; init; }

    /// <summary>科目类别</summary>
    public required AccountCategory Category { get; init; }

    /// <summary>期末余额（按余额方向取正）</summary>
    public required decimal Amount { get; init; }
}
