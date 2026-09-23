namespace App.Core.Abstractions;

/// <summary>
/// 资金日记账流水行读模型（由 `023` 收付款单聚合得到，不落流水表；口径见 specs/034-erp-cash/design.md §0.2）。
/// </summary>
public sealed record CashJournalEntryItem
{
    /// <summary>业务日期（收付款单的 SettlementDate）</summary>
    public required DateTimeOffset Date { get; init; }

    /// <summary>收付款单号（RC / PY 前缀）</summary>
    public required string SettlementNo { get; init; }

    /// <summary>摘要（收 / 付 + 往来单位名称快照）</summary>
    public required string Summary { get; init; }

    /// <summary>收款金额（付款行为 0）</summary>
    public required decimal Debit { get; init; }

    /// <summary>付款金额（收款行为 0）</summary>
    public required decimal Credit { get; init; }

    /// <summary>该笔之后的账户结余（由 IRequestHandler 逐笔滚动累计）</summary>
    public decimal Balance { get; init; }
}
