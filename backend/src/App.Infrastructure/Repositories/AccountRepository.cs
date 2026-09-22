using App.Core.Abstractions;
using App.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 会计科目仓储的 EF Core 实现（PostgreSQL）。只做数据访问，不做业务判定。
/// 一次取全量科目（科目表量级小），建树与防环判定在 Handler
/// （specs/031-erp-finance-master/design.md §3.1）。
/// </summary>
public sealed class AccountRepository : IAccountRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化会计科目仓储
    /// </summary>
    public AccountRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Account>> GetAllAsync(CancellationToken cancellationToken = default)
        // 一次取全量（含停用科目：树需完整展示，停用仅影响可选性），同级按排序升序
        => await _dbContext.Accounts.AsNoTracking()
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.Code)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        // 该查询同时服务于详情读取与编辑 / 启停 / 删除，需跟踪实体以便后续更新，故不使用 AsNoTracking
        => _dbContext.Accounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var lower = code.Trim().ToLowerInvariant();
        var query = _dbContext.Accounts.Where(a => a.Code.ToLower() == lower);
        if (excludeId is not null)
        {
            var exclude = excludeId.Value;
            query = query.Where(a => a.Id != exclude);
        }

        return query.AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> HasChildrenAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Accounts.AnyAsync(a => a.ParentId == id, cancellationToken);

    /// <inheritdoc />
    public Task<bool> IsReferencedByVoucherAsync(Guid id, CancellationToken cancellationToken = default)
        // 凭证分录表由 033-erp-general-ledger 落地：科目一旦被分录引用即禁止删除
        => _dbContext.VoucherEntries.AnyAsync(e => e.AccountId == id, cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(Account account, CancellationToken cancellationToken = default)
    {
        _dbContext.Accounts.Add(account);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Account account, CancellationToken cancellationToken = default)
    {
        _dbContext.Accounts.Update(account);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Account account, CancellationToken cancellationToken = default)
    {
        _dbContext.Accounts.Remove(account);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}