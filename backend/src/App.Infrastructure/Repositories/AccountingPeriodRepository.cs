using App.Core.Abstractions;
using App.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 会计期间仓储的 EF Core 实现（PostgreSQL）。只做数据访问，不做业务判定
/// （specs/033-erp-general-ledger/design.md §3.1）。
/// </summary>
public sealed class AccountingPeriodRepository : IAccountingPeriodRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化会计期间仓储
    /// </summary>
    public AccountingPeriodRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task<AccountingPeriod?> GetByYearMonthAsync(int year, int month, CancellationToken cancellationToken = default)
        => _dbContext.AccountingPeriods.FirstOrDefaultAsync(p => p.Year == year && p.Month == month, cancellationToken);

    /// <inheritdoc />
    public Task<AccountingPeriod?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        // 供结账 / 反结账使用，需跟踪实体以便更新
        => _dbContext.AccountingPeriods.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<AccountingPeriod>> GetAllAsync(int? year = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.AccountingPeriods.AsNoTracking().AsQueryable();
        if (year is not null)
        {
            var value = year.Value;
            query = query.Where(p => p.Year == value);
        }

        return await query
            .OrderBy(p => p.Year)
            .ThenBy(p => p.Month)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task SetStatusAsync(Guid id, PeriodStatus status, Guid? operatorId, CancellationToken cancellationToken = default)
    {
        var period = await _dbContext.AccountingPeriods.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (period is null)
        {
            return;
        }

        period.Status = status;
        if (status == PeriodStatus.Closed)
        {
            period.ClosedAt = DateTimeOffset.UtcNow;
            period.ClosedBy = operatorId;
        }
        else
        {
            period.ClosedAt = null;
            period.ClosedBy = null;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
