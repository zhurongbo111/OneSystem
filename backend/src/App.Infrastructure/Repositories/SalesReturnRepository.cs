using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 销售退货单仓储的 EF Core 实现（PostgreSQL）。只做数据访问，不做业务判定。
/// 审计字段统一由 Handler 经 ICurrentUser 获取后随方法参数 / 实体传入，仓储不感知当前用户。
/// </summary>
public sealed class SalesReturnRepository : ISalesReturnRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化销售退货单仓储
    /// </summary>
    public SalesReturnRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<SalesReturn> Items, int Total)> GetPagedAsync(
        string? keyword,
        Guid? partnerId,
        DateTimeOffset? start,
        DateTimeOffset? end,
        OrderSettlementStatus? settlement,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.SalesReturns.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lower = keyword.Trim().ToLowerInvariant();
            query = query.Where(r => r.ReturnNo.ToLower().Contains(lower) || r.PartnerName.ToLower().Contains(lower));
        }

        if (partnerId is not null)
        {
            var value = partnerId.Value;
            query = query.Where(r => r.PartnerId == value);
        }

        // 日期范围对 ReturnDate 闭区间比较（timestamptz 语义，不做时区归一化）
        if (start is not null)
        {
            var s = start.Value;
            query = query.Where(r => r.ReturnDate >= s);
        }

        if (end is not null)
        {
            var e = end.Value;
            query = query.Where(r => r.ReturnDate <= e);
        }

        if (settlement is not null)
        {
            var value = settlement.Value;
            query = query.Where(r => r.SettlementStatus == value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    /// <inheritdoc />
    public async Task<(SalesReturn? Return, IReadOnlyList<SalesReturnItem> Items)> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var salesReturn = await _dbContext.SalesReturns.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (salesReturn is null)
        {
            return (null, Array.Empty<SalesReturnItem>());
        }

        // 明细行按插入顺序（明细 Id 为顺序 Guid，与 AddRange 顺序一致）
        var items = await _dbContext.SalesReturnItems.AsNoTracking()
            .Where(i => i.ReturnId == id)
            .OrderBy(i => i.Id)
            .ToListAsync(cancellationToken);

        return (salesReturn, items);
    }

    /// <inheritdoc />
    public async Task AddAsync(SalesReturn salesReturn, IReadOnlyList<SalesReturnItem> items, CancellationToken cancellationToken = default)
    {
        _dbContext.SalesReturns.Add(salesReturn);
        _dbContext.SalesReturnItems.AddRange(items);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // 单号唯一约束冲突（PostgreSQL SQLSTATE 23505 = unique_violation）→ 包装为技术异常，由 Handler 重新生成单号重试
            throw new OrderNoConflictException(ex);
        }
    }

    /// <inheritdoc />
    public Task UpdateSettlementAsync(Guid id, OrderSettlementStatus settlement, Guid? operatorId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return _dbContext.SalesReturns
            .Where(r => r.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.SettlementStatus, settlement)
                .SetProperty(r => r.UpdatedAt, now)
                .SetProperty(r => r.UpdatedBy, operatorId),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task UpdateStatusAsync(Guid id, OrderStatus status, Guid? operatorId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return _dbContext.SalesReturns
            .Where(r => r.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, status)
                .SetProperty(r => r.UpdatedAt, now)
                .SetProperty(r => r.UpdatedBy, operatorId),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> GenerateReturnNoAsync(string prefix, DateTimeOffset returnDate, CancellationToken cancellationToken = default)
    {
        // 序号 = 当天同前缀已有单号数 + 1；唯一索引兜底并发冲突（Handler 重试）
        var dateSegment = returnDate.UtcDateTime.ToString("yyyyMMdd");
        var pattern = $"{prefix}{dateSegment}";
        var count = await _dbContext.SalesReturns.CountAsync(r => r.ReturnNo.StartsWith(pattern), cancellationToken);
        return $"{pattern}{(count + 1):D4}";
    }
}
