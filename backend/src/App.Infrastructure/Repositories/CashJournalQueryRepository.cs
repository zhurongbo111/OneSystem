using App.Core.Abstractions;
using App.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 资金日记账只读聚合查询仓储的 EF Core 实现。
/// 日记账完全由 `023` 收付款单派生——不落流水表，避免"两套流水不一致"（specs/034-erp-cash/design.md §0.2）。
/// </summary>
public sealed class CashJournalQueryRepository : ICashJournalQueryRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化资金日记账查询仓储
    /// </summary>
    public CashJournalQueryRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<(bool Exists, decimal OpeningBalance, IReadOnlyList<CashJournalEntryItem> Entries)> GetJournalAsync(
        Guid bankAccountId,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default)
    {
        var initialBalance = await _dbContext.BankAccounts.AsNoTracking()
            .Where(b => b.Id == bankAccountId)
            .Select(b => (decimal?)b.InitialBalance)
            .FirstOrDefaultAsync(cancellationToken);

        if (initialBalance is null)
        {
            return (false, 0m, Array.Empty<CashJournalEntryItem>());
        }

        // 期初 = 初始余额 + 起始日之前全部收付款净额（作废单据排除）
        var beforeStart = _dbContext.Settlements.AsNoTracking()
            .Where(s => s.BankAccountId == bankAccountId
                && s.Status == OrderStatus.Normal
                && s.SettlementDate < start);
        var beforeReceipt = await beforeStart
            .Where(s => s.Type == SettlementType.Receipt)
            .SumAsync(s => s.TotalAmount, cancellationToken);
        var beforePayment = await beforeStart
            .Where(s => s.Type == SettlementType.Payment)
            .SumAsync(s => s.TotalAmount, cancellationToken);

        var openingBalance = initialBalance.Value + beforeReceipt - beforePayment;

        // 区间流水：按业务日期升序，同一日以创建先后为准（正向增量 = 收款、负向 = 付款由 Debit / Credit 分列表达）
        var entries = await _dbContext.Settlements.AsNoTracking()
            .Where(s => s.BankAccountId == bankAccountId
                && s.Status == OrderStatus.Normal
                && s.SettlementDate >= start
                && s.SettlementDate <= end)
            .OrderBy(s => s.SettlementDate)
            .ThenBy(s => s.CreatedAt)
            .Select(s => new CashJournalEntryItem
            {
                Date = s.SettlementDate,
                SettlementNo = s.SettlementNo,
                Summary = s.Type == SettlementType.Receipt
                    ? $"收款-{s.PartnerName}"
                    : $"付款-{s.PartnerName}",
                Debit = s.Type == SettlementType.Receipt ? s.TotalAmount : 0m,
                Credit = s.Type == SettlementType.Payment ? s.TotalAmount : 0m,
            })
            .ToListAsync(cancellationToken);

        return (true, openingBalance, entries);
    }
}
