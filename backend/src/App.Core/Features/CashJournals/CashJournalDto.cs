namespace App.Core.Features.CashJournals;

/// <summary>
/// 资金日记账出参模型（期初 + 流水 + 期末；流水由 `023` 收付款单派生，不落流水表）
/// </summary>
public sealed class CashJournalDto
{
    /// <summary>资金账户 id</summary>
    public string BankAccountId { get; init; } = string.Empty;

    /// <summary>期初余额（初始余额 + 起始日之前的收付款净额）</summary>
    public decimal OpeningBalance { get; init; }

    /// <summary>期末余额（期初 + 区间收 − 区间付）</summary>
    public decimal ClosingBalance { get; init; }

    /// <summary>流水行（按业务日期升序）</summary>
    public required IReadOnlyList<CashJournalEntryDto> Entries { get; init; }
}

/// <summary>
/// 资金日记账流水行出参模型（收款记借方 `Debit`、付款记贷方方向记 `Credit`，`Balance` 为逐笔结余）
/// </summary>
public sealed class CashJournalEntryDto
{
    /// <summary>业务日期</summary>
    public DateTimeOffset Date { get; init; }

    /// <summary>收付款单号</summary>
    public string SettlementNo { get; init; } = string.Empty;

    /// <summary>摘要</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>收款金额（付款行为 0）</summary>
    public decimal Debit { get; init; }

    /// <summary>付款金额（收款行为 0）</summary>
    public decimal Credit { get; init; }

    /// <summary>本笔之后的账户结余</summary>
    public decimal Balance { get; init; }
}
