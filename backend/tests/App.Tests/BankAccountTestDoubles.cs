using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Tests;

/// <summary>
/// 行为型资金账户仓储假实现（内存存储 + 记录调用），供资金账户用例单测与 `023` 收付款创建用例
/// 解析资金账户使用（规避 InMemory 提供程序的行为差异，见后端规则 §9）。
/// </summary>
internal sealed class FakeBankAccountRepository : IBankAccountRepository
{
    private readonly Dictionary<Guid, BankAccount> _accounts = [];

    /// <summary>已执行的分页查询入参</summary>
    public List<(string? Keyword, BankAccountType? Type, BankAccountStatus? Status, int Page, int PageSize)> PagedQueries
    { get; } = [];

    /// <summary>分页查询返回的行（由用例预置）</summary>
    public IReadOnlyList<BankAccountListItem> PagedItems { get; set; } = Array.Empty<BankAccountListItem>();

    /// <summary>分页查询返回的总数（由用例预置）</summary>
    public int PagedTotal { get; set; }

    /// <summary>余额聚合返回值（由用例预置）</summary>
    public IReadOnlyList<BankAccountBalanceItem> Balances { get; set; } = Array.Empty<BankAccountBalanceItem>();

    /// <summary>删除保护的引用判定返回值（由用例预置）</summary>
    public bool Referenced { get; set; }

    /// <summary>预置一个资金账户（供详情 / 编辑 / 启停 / 删除用例）</summary>
    public void Seed(BankAccount bankAccount) => _accounts[bankAccount.Id] = bankAccount;

    /// <summary>读取账户当前值（供断言编辑 / 启停结果）</summary>
    public BankAccount Get(Guid id) => _accounts[id];

    public Task<(IReadOnlyList<BankAccountListItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        BankAccountType? type,
        BankAccountStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        PagedQueries.Add((keyword, type, status, page, pageSize));
        return Task.FromResult((PagedItems, PagedTotal));
    }

    public Task<BankAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_accounts.TryGetValue(id, out var account) ? account : null);

    public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
        => Task.FromResult(_accounts.Values.Any(a =>
            string.Equals(a.Code, code.Trim(), StringComparison.OrdinalIgnoreCase)
            && (excludeId is null || a.Id != excludeId.Value)));

    public Task<IReadOnlyList<BankAccountBalanceItem>> GetBalancesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Balances);

    public Task<bool> IsReferencedAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Referenced);

    public Task AddAsync(BankAccount bankAccount, CancellationToken cancellationToken = default)
    {
        _accounts[bankAccount.Id] = bankAccount;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(BankAccount bankAccount, CancellationToken cancellationToken = default)
    {
        _accounts[bankAccount.Id] = bankAccount;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(BankAccount bankAccount, CancellationToken cancellationToken = default)
    {
        _accounts.Remove(bankAccount.Id);
        return Task.CompletedTask;
    }
}

/// <summary>
/// 行为型资金日记账只读聚合仓储假实现（记录入参 + 预置返回），供日记账用例断言期初 / 期末与逐笔结余。
/// </summary>
internal sealed class FakeCashJournalQueryRepository : ICashJournalQueryRepository
{
    /// <summary>已执行的查询入参</summary>
    public List<(Guid BankAccountId, DateTimeOffset Start, DateTimeOffset End)> JournalQueries { get; } = [];

    /// <summary>预置返回：账户是否存在</summary>
    public bool Exists { get; set; } = true;

    /// <summary>预置返回：期初余额</summary>
    public decimal OpeningBalance { get; set; }

    /// <summary>预置返回：区间流水</summary>
    public IReadOnlyList<CashJournalEntryItem> Entries { get; set; } = Array.Empty<CashJournalEntryItem>();

    public Task<(bool Exists, decimal OpeningBalance, IReadOnlyList<CashJournalEntryItem> Entries)> GetJournalAsync(
        Guid bankAccountId,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default)
    {
        JournalQueries.Add((bankAccountId, start, end));
        return Task.FromResult((Exists, OpeningBalance, Entries));
    }
}
