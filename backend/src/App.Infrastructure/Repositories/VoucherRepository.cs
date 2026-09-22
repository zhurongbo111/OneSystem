using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 记账凭证仓储的 EF Core 实现（PostgreSQL）。只做数据访问，不做业务判定。
/// 审计字段由 Handler 经 ICurrentUser 获取后随方法参数 / 实体传入，仓储不感知当前用户
/// （specs/033-erp-general-ledger/design.md §3.1）。
/// </summary>
public sealed class VoucherRepository : IVoucherRepository
{
    /// <summary>PostgreSQL unique_violation 的 SQLSTATE</summary>
    private const string UniqueViolationSqlState = "23505";

    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化记账凭证仓储
    /// </summary>
    public VoucherRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task AddAsync(Voucher voucher, IReadOnlyList<VoucherEntry> entries, CancellationToken cancellationToken = default)
    {
        _dbContext.Vouchers.Add(voucher);
        _dbContext.VoucherEntries.AddRange(entries);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException { SqlState: UniqueViolationSqlState })
        {
            // 凭证号唯一约束冲突（并发窗口）：交由 Handler 重新生成凭证号重试（语义同 OrderNoConflictException）
            throw new OrderNoConflictException(ex);
        }
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<Voucher> Items, int Total)> GetPagedAsync(
        int? year,
        int? month,
        VoucherSourceType? sourceType,
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Vouchers.AsNoTracking().AsQueryable();

        if (year is not null && month is not null)
        {
            var (start, end) = PeriodRange(year.Value, month.Value);
            query = query.Where(v => v.VoucherDate >= start && v.VoucherDate < end);
        }

        if (sourceType is not null)
        {
            var value = sourceType.Value;
            query = query.Where(v => v.SourceType == value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lower = keyword.Trim().ToLowerInvariant();
            query = query.Where(v => v.VoucherNo.ToLower().Contains(lower) || v.Summary.ToLower().Contains(lower));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(v => v.VoucherDate)
            .ThenByDescending(v => v.VoucherNo)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    /// <inheritdoc />
    public async Task<(Voucher? Voucher, IReadOnlyList<VoucherEntry> Entries)> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var voucher = await _dbContext.Vouchers.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

        if (voucher is null)
        {
            return (null, Array.Empty<VoucherEntry>());
        }

        var entries = await _dbContext.VoucherEntries.AsNoTracking()
            .Where(e => e.VoucherId == id)
            .OrderBy(e => e.LineNo)
            .ToListAsync(cancellationToken);

        return (voucher, entries);
    }

    /// <inheritdoc />
    public async Task<int> VoidAsync(Guid id, Guid? operatorId, CancellationToken cancellationToken = default)
    {
        var voucher = await _dbContext.Vouchers.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        if (voucher is null || voucher.Status == VoucherStatus.Voided)
        {
            return 0;
        }

        voucher.Status = VoucherStatus.Voided;
        voucher.UpdatedAt = DateTimeOffset.UtcNow;
        voucher.UpdatedBy = operatorId;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return 1;
    }

    /// <inheritdoc />
    public async Task<int> VoidBySourceAsync(VoucherSourceType sourceType, Guid sourceId, Guid? operatorId, CancellationToken cancellationToken = default)
    {
        var vouchers = await _dbContext.Vouchers
            .Where(v => v.SourceType == sourceType && v.SourceId == sourceId && v.Status == VoucherStatus.Posted)
            .ToListAsync(cancellationToken);

        if (vouchers.Count == 0)
        {
            return 0;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var voucher in vouchers)
        {
            voucher.Status = VoucherStatus.Voided;
            voucher.UpdatedAt = now;
            voucher.UpdatedBy = operatorId;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return vouchers.Count;
    }

    /// <inheritdoc />
    public async Task<string> GenerateNoAsync(DateTimeOffset voucherDate, CancellationToken cancellationToken = default)
    {
        // 序号 = 该期间已有凭证号数 + 1；唯一索引兜底并发冲突（Handler 重试）
        var pattern = $"{VoucherFieldConstraints.NoPrefix}{voucherDate:yyyyMM}-";
        var count = await _dbContext.Vouchers.CountAsync(v => v.VoucherNo.StartsWith(pattern), cancellationToken);
        return $"{pattern}{(count + 1):D4}";
    }

    /// <summary>期间首日（含）与次月首日（不含），用于 VoucherDate 闭开区间过滤</summary>
    private static (DateTimeOffset Start, DateTimeOffset End) PeriodRange(int year, int month)
    {
        var start = new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero);
        return (start, start.AddMonths(1));
    }
}
