using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 调拨单仓储的 EF Core 实现（PostgreSQL）。只做数据访问，不做业务判定。
/// 审计字段统一由 Handler 经 ICurrentUser 获取后随方法参数 / 实体传入，仓储不感知当前用户。
/// </summary>
public sealed class TransferRepository : ITransferRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化调拨单仓储
    /// </summary>
    public TransferRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<Transfer> Items, int Total)> GetPagedAsync(
        string? keyword,
        Guid? fromWarehouseId,
        Guid? toWarehouseId,
        DateTimeOffset? start,
        DateTimeOffset? end,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Transfers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lower = keyword.Trim().ToLowerInvariant();
            query = query.Where(t => t.TransferNo.ToLower().Contains(lower));
        }

        // 转出仓筛选（038）
        if (fromWarehouseId is not null)
        {
            var value = fromWarehouseId.Value;
            query = query.Where(t => t.FromWarehouseId == value);
        }

        // 转入仓筛选（038）
        if (toWarehouseId is not null)
        {
            var value = toWarehouseId.Value;
            query = query.Where(t => t.ToWarehouseId == value);
        }

        // 日期范围对 TransferDate 闭区间比较（timestamptz 语义，不做时区归一化）
        if (start is not null)
        {
            var s = start.Value;
            query = query.Where(t => t.TransferDate >= s);
        }

        if (end is not null)
        {
            var e = end.Value;
            query = query.Where(t => t.TransferDate <= e);
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
    public async Task<(Transfer? Transfer, IReadOnlyList<TransferItem> Items)> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var transfer = await _dbContext.Transfers.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (transfer is null)
        {
            return (null, Array.Empty<TransferItem>());
        }

        // 明细行按插入顺序（明细 Id 为顺序 Guid，与 AddRange 顺序一致）
        var items = await _dbContext.TransferItems.AsNoTracking()
            .Where(i => i.TransferId == id)
            .OrderBy(i => i.Id)
            .ToListAsync(cancellationToken);

        return (transfer, items);
    }

    /// <inheritdoc />
    public async Task AddAsync(Transfer transfer, IReadOnlyList<TransferItem> items, CancellationToken cancellationToken = default)
    {
        _dbContext.Transfers.Add(transfer);
        _dbContext.TransferItems.AddRange(items);
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
    public Task UpdateStatusAsync(Guid id, OrderStatus status, Guid? operatorId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return _dbContext.Transfers
            .Where(t => t.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, status)
                .SetProperty(t => t.UpdatedAt, now)
                .SetProperty(t => t.UpdatedBy, operatorId),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> GenerateTransferNoAsync(DateTimeOffset transferDate, CancellationToken cancellationToken = default)
    {
        // 序号 = 当天同前缀已有单号数 + 1；唯一索引兜底并发冲突（Handler 重试，见 design.md §3.1）
        const string prefix = "TR";
        var dateSegment = transferDate.UtcDateTime.ToString("yyyyMMdd");
        var pattern = $"{prefix}{dateSegment}";
        var count = await _dbContext.Transfers.CountAsync(t => t.TransferNo.StartsWith(pattern), cancellationToken);
        return $"{pattern}{(count + 1):D4}";
    }
}
