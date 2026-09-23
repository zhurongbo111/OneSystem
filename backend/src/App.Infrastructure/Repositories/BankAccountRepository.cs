using App.Core.Abstractions;
using App.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 资金账户仓储的 EF Core 实现（PostgreSQL）。只做数据访问，不做业务判定
/// （specs/034-erp-cash/design.md §3.1）。
/// </summary>
public sealed class BankAccountRepository : IBankAccountRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化资金账户仓储
    /// </summary>
    public BankAccountRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<BankAccountListItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        BankAccountType? type,
        BankAccountStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.BankAccounts.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lower = keyword.Trim().ToLowerInvariant();
            query = query.Where(b => b.Code.ToLower().Contains(lower) || b.Name.ToLower().Contains(lower));
        }

        if (type is not null)
        {
            var value = type.Value;
            query = query.Where(b => b.Type == value);
        }

        if (status is not null)
        {
            var value = status.Value;
            query = query.Where(b => b.Status == value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(b => b.CreatedAt)
            .ThenBy(b => b.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new BankAccountListItem
            {
                Id = b.Id,
                Code = b.Code,
                Name = b.Name,
                Type = b.Type,
                BankName = b.BankName,
                AccountNo = b.AccountNo,
                InitialBalance = b.InitialBalance,
                Status = b.Status,
                Remark = b.Remark,
                // 派生余额（口径见 design.md §0.2）：初始余额 + Σ 收款 − Σ 付款，只计未作废收付款单。
                // 两个 Sum 均为关联子查询，必须内联书写（自定义方法无法被 EF 翻译为 SQL）
                Balance = b.InitialBalance
                    + _dbContext.Settlements.Where(s => s.BankAccountId == b.Id
                        && s.Status == OrderStatus.Normal
                        && s.Type == SettlementType.Receipt).Sum(s => s.TotalAmount)
                    - _dbContext.Settlements.Where(s => s.BankAccountId == b.Id
                        && s.Status == OrderStatus.Normal
                        && s.Type == SettlementType.Payment).Sum(s => s.TotalAmount),
                CreatedAt = b.CreatedAt,
                UpdatedAt = b.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    /// <inheritdoc />
    public Task<BankAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        // 该查询同时服务于详情读取与编辑 / 启停 / 删除，需跟踪实体以便后续更新，故不使用 AsNoTracking
        => _dbContext.BankAccounts.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var lower = code.Trim().ToLowerInvariant();
        var query = _dbContext.BankAccounts.Where(b => b.Code.ToLower() == lower);
        if (excludeId is not null)
        {
            var exclude = excludeId.Value;
            query = query.Where(b => b.Id != exclude);
        }

        return query.AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BankAccountBalanceItem>> GetBalancesAsync(CancellationToken cancellationToken = default)
        => await _dbContext.BankAccounts.AsNoTracking()
            .Select(b => new BankAccountBalanceItem
            {
                Id = b.Id,
                Code = b.Code,
                Name = b.Name,
                Type = b.Type,
                Status = b.Status,
                // 派生余额（口径同上，内联以便 EF 翻译为关联子查询）
                Balance = b.InitialBalance
                    + _dbContext.Settlements.Where(s => s.BankAccountId == b.Id
                        && s.Status == OrderStatus.Normal
                        && s.Type == SettlementType.Receipt).Sum(s => s.TotalAmount)
                    - _dbContext.Settlements.Where(s => s.BankAccountId == b.Id
                        && s.Status == OrderStatus.Normal
                        && s.Type == SettlementType.Payment).Sum(s => s.TotalAmount),
            })
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<bool> IsReferencedAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Settlements.AnyAsync(s => s.BankAccountId == id, cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(BankAccount bankAccount, CancellationToken cancellationToken = default)
    {
        _dbContext.BankAccounts.Add(bankAccount);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(BankAccount bankAccount, CancellationToken cancellationToken = default)
    {
        _dbContext.BankAccounts.Update(bankAccount);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(BankAccount bankAccount, CancellationToken cancellationToken = default)
    {
        _dbContext.BankAccounts.Remove(bankAccount);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
