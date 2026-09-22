using App.Core.Abstractions;
using App.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 财务报表只读聚合仓储的 EF Core 实现（PostgreSQL）：
/// 按期间取已过账凭证分录，把末级科目发生额向上汇总到一级科目，再按科目余额方向折算期初 / 期末
/// （口径见 specs/033-erp-general-ledger/design.md §0.4；不落物化表）。
/// </summary>
public sealed class FinancialReportQueryRepository : IFinancialReportQueryRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化财务报表查询仓储
    /// </summary>
    public FinancialReportQueryRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AccountBalanceItem>> GetAccountBalancesAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        var (roots, aggregates) = await AggregateByRootAsync(year, month, cancellationToken);

        return roots
            .Select(root =>
            {
                var aggregate = aggregates[root.Id];
                return new AccountBalanceItem
                {
                    AccountId = root.Id,
                    Code = root.Code,
                    Name = root.Name,
                    Category = root.Category,
                    Direction = root.Direction,
                    OpeningBalance = ToClosing(aggregate.OpeningDebitNet, root.Direction),
                    PeriodDebit = aggregate.PeriodDebit,
                    PeriodCredit = aggregate.PeriodCredit,
                    ClosingBalance = ToClosing(aggregate.OpeningDebitNet + aggregate.PeriodDebitNet, root.Direction),
                };
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BalanceSheetItem>> GetBalanceSheetAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        var (roots, aggregates) = await AggregateByRootAsync(year, month, cancellationToken);

        return roots
            .Select(root =>
            {
                var aggregate = aggregates[root.Id];
                return new BalanceSheetItem
                {
                    AccountId = root.Id,
                    Code = root.Code,
                    Name = root.Name,
                    Category = root.Category,
                    Amount = ToClosing(aggregate.OpeningDebitNet + aggregate.PeriodDebitNet, root.Direction),
                };
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IncomeStatementItem>> GetIncomeStatementAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        var (roots, aggregates) = await AggregateByRootAsync(year, month, cancellationToken);

        return roots
            .Where(root => root.Category == AccountCategory.ProfitLoss)
            .Select(root =>
            {
                // 本期按方向净额：借方向科目为「借 − 贷」，贷方向科目为「贷 − 借」
                var aggregate = aggregates[root.Id];
                var net = root.Direction == AccountDirection.Debit ? aggregate.PeriodDebitNet : -aggregate.PeriodDebitNet;
                return new IncomeStatementItem
                {
                    AccountId = root.Id,
                    Code = root.Code,
                    Name = root.Name,
                    IsRevenue = net >= 0m,
                    Amount = Math.Abs(net),
                };
            })
            .Where(item => item.Amount != 0m)
            .ToList();
    }

    /// <summary>
    /// 按一级科目聚合：返回一级科目列表（按编码升序）与各一级科目的期初 / 本期聚合值
    /// </summary>
    private async Task<(IReadOnlyList<Account> Roots, Dictionary<Guid, RootAggregate> Aggregates)> AggregateByRootAsync(
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        var (start, end) = PeriodRange(year, month);
        var accounts = await _dbContext.Accounts.AsNoTracking().ToListAsync(cancellationToken);
        var accountsById = accounts.ToDictionary(a => a.Id);

        var roots = accounts
            .Where(a => a.ParentId is null)
            .OrderBy(a => a.Code)
            .ToList();
        var aggregates = roots.ToDictionary(r => r.Id, _ => new RootAggregate());

        var openingRows = await AggregateEntriesAsync(null, start, cancellationToken);
        var periodRows = await AggregateEntriesAsync(start, end, cancellationToken);

        foreach (var row in openingRows)
        {
            var rootId = ResolveRootId(row.AccountId, accountsById);
            if (rootId is not null)
            {
                aggregates[rootId.Value].OpeningDebitNet += row.Debit - row.Credit;
            }
        }

        foreach (var row in periodRows)
        {
            var rootId = ResolveRootId(row.AccountId, accountsById);
            if (rootId is not null)
            {
                var aggregate = aggregates[rootId.Value];
                aggregate.PeriodDebit += row.Debit;
                aggregate.PeriodCredit += row.Credit;
            }
        }

        return (roots, aggregates);
    }

    /// <summary>按科目聚合已过账分录的发生额（<paramref name="start"/> 含、<paramref name="end"/> 不含，可空）</summary>
    private async Task<List<EntryRow>> AggregateEntriesAsync(
        DateTimeOffset? start,
        DateTimeOffset? end,
        CancellationToken cancellationToken)
    {
        var query = from e in _dbContext.VoucherEntries.AsNoTracking()
                    join v in _dbContext.Vouchers.AsNoTracking() on e.VoucherId equals v.Id
                    where v.Status == VoucherStatus.Posted
                    select new { e.AccountId, e.Debit, e.Credit, v.VoucherDate };

        if (start is not null)
        {
            var from = start.Value;
            query = query.Where(x => x.VoucherDate >= from);
        }

        if (end is not null)
        {
            var to = end.Value;
            query = query.Where(x => x.VoucherDate < to);
        }

        var rows = await query
            .GroupBy(x => x.AccountId)
            .Select(g => new { AccountId = g.Key, Debit = g.Sum(x => x.Debit), Credit = g.Sum(x => x.Credit) })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new EntryRow(r.AccountId, r.Debit, r.Credit)).ToList();
    }

    /// <summary>沿父链上溯到一级科目 id；科目不属于任何一级时返回 <c>null</c></summary>
    private static Guid? ResolveRootId(Guid accountId, IReadOnlyDictionary<Guid, Account> accountsById)
    {
        var current = accountId;
        // 科目树深度有限，最多上溯到根（自引用环由 031 的防环校验保证不出现）
        while (true)
        {
            if (!accountsById.TryGetValue(current, out var account))
            {
                return null;
            }

            if (account.ParentId is null)
            {
                return account.Id;
            }

            current = account.ParentId.Value;
        }
    }

    /// <summary>按余额方向把「借方净额」折算为科目余额（借方向取原值，贷方向取反）</summary>
    private static decimal ToClosing(decimal debitNet, AccountDirection direction)
        => direction == AccountDirection.Debit ? debitNet : -debitNet;

    /// <summary>期间首日（含）与次月首日（不含）</summary>
    private static (DateTimeOffset Start, DateTimeOffset End) PeriodRange(int year, int month)
    {
        var start = new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero);
        return (start, start.AddMonths(1));
    }

    /// <summary>分录聚合行（借方净额 = 借 − 贷）</summary>
    private sealed record EntryRow(Guid AccountId, decimal Debit, decimal Credit);

    /// <summary>一级科目的聚合容器</summary>
    private sealed class RootAggregate
    {
        /// <summary>期初借方净额（该一级科目下全部末级科目，期间首日之前）</summary>
        public decimal OpeningDebitNet { get; set; }

        /// <summary>本期借方发生额</summary>
        public decimal PeriodDebit { get; set; }

        /// <summary>本期贷方发生额</summary>
        public decimal PeriodCredit { get; set; }

        /// <summary>本期借方净额（借 − 贷）</summary>
        public decimal PeriodDebitNet => PeriodDebit - PeriodCredit;
    }
}
