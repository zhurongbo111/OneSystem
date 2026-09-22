namespace App.Core.Features.GeneralLedger;

/// <summary>
/// 科目余额表行出参模型（按一级科目列示；枚举以整型输出）
/// </summary>
public sealed class AccountBalanceItemDto
{
    /// <summary>一级科目 id</summary>
    public required string AccountId { get; init; }

    /// <summary>一级科目编码</summary>
    public required string Code { get; init; }

    /// <summary>一级科目名称</summary>
    public required string Name { get; init; }

    /// <summary>科目类别（1 资产 / 2 负债 / 3 权益 / 4 成本 / 5 损益）</summary>
    public required int Category { get; init; }

    /// <summary>余额方向（1 借 / 2 贷）</summary>
    public required int Direction { get; init; }

    /// <summary>期初余额（按余额方向）</summary>
    public required decimal OpeningBalance { get; init; }

    /// <summary>本期借方发生额</summary>
    public required decimal PeriodDebit { get; init; }

    /// <summary>本期贷方发生额</summary>
    public required decimal PeriodCredit { get; init; }

    /// <summary>期末余额（按余额方向）</summary>
    public required decimal ClosingBalance { get; init; }
}

/// <summary>
/// 资产负债表出参模型（按一级科目列示，损益类单列供「本年利润」行展示）
/// </summary>
public sealed class BalanceSheetDto
{
    /// <summary>年</summary>
    public required int Year { get; init; }

    /// <summary>月</summary>
    public required int Month { get; init; }

    /// <summary>资产类科目行</summary>
    public required IReadOnlyList<BalanceSheetItemDto> Assets { get; init; }

    /// <summary>负债类科目行</summary>
    public required IReadOnlyList<BalanceSheetItemDto> Liabilities { get; init; }

    /// <summary>所有者权益类科目行</summary>
    public required IReadOnlyList<BalanceSheetItemDto> Equities { get; init; }

    /// <summary>损益类科目行（构成本年利润，未结转）</summary>
    public required IReadOnlyList<BalanceSheetItemDto> ProfitLossItems { get; init; }

    /// <summary>资产合计</summary>
    public required decimal TotalAssets { get; init; }

    /// <summary>负债合计</summary>
    public required decimal TotalLiabilities { get; init; }

    /// <summary>所有者权益合计</summary>
    public required decimal TotalEquities { get; init; }

    /// <summary>本年利润（损益类期末净额合计，未结转）</summary>
    public required decimal CurrentProfit { get; init; }

    /// <summary>负债 + 权益 + 本年利润（恒等于资产合计）</summary>
    public required decimal TotalLiabilitiesAndEquity { get; init; }
}

/// <summary>资产负债表行出参模型</summary>
public sealed class BalanceSheetItemDto
{
    /// <summary>一级科目 id</summary>
    public required string AccountId { get; init; }

    /// <summary>一级科目编码</summary>
    public required string Code { get; init; }

    /// <summary>一级科目名称</summary>
    public required string Name { get; init; }

    /// <summary>期末余额（按余额方向取正）</summary>
    public required decimal Amount { get; init; }
}

/// <summary>
/// 利润表出参模型（损益类科目本期发生额，按净额符号分收入 / 成本费用两侧）
/// </summary>
public sealed class IncomeStatementDto
{
    /// <summary>年</summary>
    public required int Year { get; init; }

    /// <summary>月</summary>
    public required int Month { get; init; }

    /// <summary>收入侧科目行</summary>
    public required IReadOnlyList<IncomeStatementItemDto> RevenueItems { get; init; }

    /// <summary>成本费用侧科目行</summary>
    public required IReadOnlyList<IncomeStatementItemDto> CostItems { get; init; }

    /// <summary>收入合计</summary>
    public required decimal TotalRevenue { get; init; }

    /// <summary>成本费用合计</summary>
    public required decimal TotalCost { get; init; }

    /// <summary>净利润 = 收入合计 − 成本费用合计</summary>
    public required decimal NetProfit { get; init; }
}

/// <summary>利润表行出参模型</summary>
public sealed class IncomeStatementItemDto
{
    /// <summary>一级科目 id</summary>
    public required string AccountId { get; init; }

    /// <summary>一级科目编码</summary>
    public required string Code { get; init; }

    /// <summary>一级科目名称</summary>
    public required string Name { get; init; }

    /// <summary>本期金额（正数）</summary>
    public required decimal Amount { get; init; }
}
