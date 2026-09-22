using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 科目余额表读模型（仓储出参契约，按**一级科目**列示：末级余额向上汇总）。
/// 期初 / 期末按科目余额方向（<see cref="Direction"/>）计算，见 specs/033-erp-general-ledger/design.md §0.4。
/// </summary>
public sealed record AccountBalanceItem
{
    /// <summary>一级科目 id</summary>
    public required Guid AccountId { get; init; }

    /// <summary>一级科目编码</summary>
    public required string Code { get; init; }

    /// <summary>一级科目名称</summary>
    public required string Name { get; init; }

    /// <summary>科目类别</summary>
    public required AccountCategory Category { get; init; }

    /// <summary>余额方向（借 / 贷）</summary>
    public required AccountDirection Direction { get; init; }

    /// <summary>期初余额（期间首日之前全部已过账分录的按方向净额）</summary>
    public required decimal OpeningBalance { get; init; }

    /// <summary>本期借方发生额</summary>
    public required decimal PeriodDebit { get; init; }

    /// <summary>本期贷方发生额</summary>
    public required decimal PeriodCredit { get; init; }

    /// <summary>期末余额（按余额方向）</summary>
    public required decimal ClosingBalance { get; init; }
}
