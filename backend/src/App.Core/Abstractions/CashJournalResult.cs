namespace App.Core.Abstractions;

/// <summary>
/// 资金日记账结果读模型（期初 + 流水 + 期末；期末由 <see cref="IRequestHandler{TRequest,TResponse}"/> 之外的用例层计算，
/// 期初 / 流水的取数口径见 specs/034-erp-cash/design.md §0.2）。
/// </summary>
public sealed record CashJournalResult
{
    /// <summary>期初余额（初始余额 + start 之前的全部收付款净额）</summary>
    public required decimal OpeningBalance { get; init; }

    /// <summary>区间内流水（按业务日期升序）</summary>
    public required IReadOnlyList<CashJournalEntryItem> Entries { get; init; }

    /// <summary>期末余额（期初 + 区间收 − 区间付）</summary>
    public required decimal ClosingBalance { get; init; }
}
