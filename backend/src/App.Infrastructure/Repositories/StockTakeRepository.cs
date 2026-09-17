using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 盘点 / 期初建账单据仓储的 EF Core 实现（PostgreSQL）。只做数据访问，不做业务判定。
/// 审计字段统一由 Handler 经 ICurrentUser 获取后随实体传入，仓储不感知当前用户。
/// </summary>
public sealed class StockTakeRepository : IStockTakeRepository
{
    private const string TakeNoPrefix = "ST";

    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化盘点单仓储
    /// </summary>
    public StockTakeRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task AddAsync(StockTake take, IReadOnlyList<StockTakeItem> items, CancellationToken cancellationToken = default)
    {
        _dbContext.StockTakes.Add(take);
        _dbContext.StockTakeItems.AddRange(items);
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
    public async Task<(IReadOnlyList<StockTake> Items, int Total)> GetPagedAsync(
        string? keyword,
        StockTakeType? type,
        DateTimeOffset? start,
        DateTimeOffset? end,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.StockTakes.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lower = keyword.Trim().ToLowerInvariant();
            query = query.Where(t => t.TakeNo.ToLower().Contains(lower));
        }

        if (type is not null)
        {
            var value = type.Value;
            query = query.Where(t => t.Type == value);
        }

        // 日期范围对 TakeDate 闭区间比较（timestamptz 语义，不做时区归一化）
        if (start is not null)
        {
            var s = start.Value;
            query = query.Where(t => t.TakeDate >= s);
        }

        if (end is not null)
        {
            var e = end.Value;
            query = query.Where(t => t.TakeDate <= e);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    /// <inheritdoc />
    public async Task<(StockTake? Take, IReadOnlyList<StockTakeItem> Items)> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var take = await _dbContext.StockTakes.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (take is null)
        {
            return (null, Array.Empty<StockTakeItem>());
        }

        // 明细行按插入顺序（明细 Id 为顺序 Guid，与 AddRange 顺序一致）
        var items = await _dbContext.StockTakeItems.AsNoTracking()
            .Where(i => i.StockTakeId == id)
            .OrderBy(i => i.Id)
            .ToListAsync(cancellationToken);

        return (take, items);
    }

    /// <inheritdoc />
    public async Task<string> GenerateTakeNoAsync(DateTimeOffset takeDate, CancellationToken cancellationToken = default)
    {
        // 序号 = 当天同前缀已有单号数 + 1；唯一索引兜底并发冲突（Handler 重试，见 design.md §3.1）
        var dateSegment = takeDate.UtcDateTime.ToString("yyyyMMdd");
        var pattern = $"{TakeNoPrefix}{dateSegment}";
        var count = await _dbContext.StockTakes.CountAsync(t => t.TakeNo.StartsWith(pattern), cancellationToken);
        return $"{pattern}{(count + 1):D4}";
    }
}
