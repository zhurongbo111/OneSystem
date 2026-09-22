namespace App.Core.Abstractions;

/// <summary>
/// 利润表行读模型（仓储出参契约，按**一级科目**列示）。
/// <see cref="Amount"/> 恒为正数：<see cref="IsRevenue"/> 为 true 时归「收入」侧，否则归「成本费用」侧
/// （按余额方向计算的本期净额符号判定，见 specs/033-erp-general-ledger/design.md §0.4）。
/// </summary>
public sealed record IncomeStatementItem
{
    /// <summary>一级科目 id</summary>
    public required Guid AccountId { get; init; }

    /// <summary>一级科目编码</summary>
    public required string Code { get; init; }

    /// <summary>一级科目名称</summary>
    public required string Name { get; init; }

    /// <summary>是否收入侧（false 表示成本费用侧）</summary>
    public required bool IsRevenue { get; init; }

    /// <summary>本期金额（正数）</summary>
    public required decimal Amount { get; init; }
}
