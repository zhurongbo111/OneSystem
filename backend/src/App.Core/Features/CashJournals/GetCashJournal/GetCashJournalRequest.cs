using App.Core.Abstractions;

namespace App.Core.Features.CashJournals.GetCashJournal;

/// <summary>
/// 资金日记账查询请求（Query 参数绑定；按账户 + 业务日期闭区间）
/// </summary>
public sealed class GetCashJournalRequest : IRequest<CashJournalDto>
{
    /// <summary>资金账户 id（必填）</summary>
    public Guid BankAccountId { get; init; }

    /// <summary>起始业务日期（含）</summary>
    public DateTimeOffset Start { get; init; }

    /// <summary>结束业务日期（含）</summary>
    public DateTimeOffset End { get; init; }
}
