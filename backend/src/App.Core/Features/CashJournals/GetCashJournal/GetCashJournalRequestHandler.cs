using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.CashJournals.GetCashJournal;

/// <summary>
/// 资金日记账查询用例：账户存在（40400）→ 取期初与区间流水 → 逐笔滚动累计结余与期末。
/// 只读聚合——日记账由收付款单派生，不落流水表（specs/034-erp-cash/design.md §0.2）
/// </summary>
public sealed class GetCashJournalRequestHandler : IRequestHandler<GetCashJournalRequest, CashJournalDto>
{
    private readonly ICashJournalQueryRepository _cashJournalQueryRepository;

    /// <summary>
    /// 初始化资金日记账查询用例处理器
    /// </summary>
    public GetCashJournalRequestHandler(ICashJournalQueryRepository cashJournalQueryRepository)
    {
        _cashJournalQueryRepository = cashJournalQueryRepository;
    }

    /// <summary>
    /// 处理资金日记账查询请求
    /// </summary>
    /// <param name="request">查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<CashJournalDto> HandleAsync(GetCashJournalRequest request, CancellationToken cancellationToken = default)
    {
        var (exists, openingBalance, rows) = await _cashJournalQueryRepository.GetJournalAsync(
            request.BankAccountId,
            request.Start,
            request.End,
            cancellationToken);

        if (!exists)
        {
            throw new BusinessException(ErrorCode.NotFound, "资金账户不存在");
        }

        // 逐笔滚动结余：期初 + Σ(收 − 付)；空区间时期末 = 期初
        var running = openingBalance;
        var entries = new List<CashJournalEntryDto>(rows.Count);
        foreach (var row in rows)
        {
            running += row.Debit - row.Credit;
            entries.Add(new CashJournalEntryDto
            {
                Date = row.Date,
                SettlementNo = row.SettlementNo,
                Summary = row.Summary,
                Debit = row.Debit,
                Credit = row.Credit,
                Balance = running,
            });
        }

        return new CashJournalDto
        {
            BankAccountId = request.BankAccountId.ToString(),
            OpeningBalance = openingBalance,
            ClosingBalance = running,
            Entries = entries,
        };
    }
}
