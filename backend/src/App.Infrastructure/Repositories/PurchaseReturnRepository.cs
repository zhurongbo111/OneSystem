using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 采购退货单仓储的 EF Core 实现（PostgreSQL）。只做数据访问，不做业务判定。
/// 审计字段统一由 Handler 经 ICurrentUser 获取后随方法参数 / 实体传入，仓储不感知当前用户。
/// </summary>
public sealed class PurchaseReturnRepository : IPurchaseReturnRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化采购退货单仓储
    /// </summary>
    public PurchaseReturnRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<PurchaseReturn> Items, int Total)> GetPagedAsync(
        string? keyword,
        Guid? partnerId,
        DateTimeOffset? start,
        DateTimeOffset? end,
        OrderSettlementStatus? settlement,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.PurchaseReturns.AsNoTracking().AsQueryable();

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
    public async Task<(PurchaseReturn? Return, IReadOnlyList<PurchaseReturnItem> Items)> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var purchaseReturn = await _dbContext.PurchaseReturns.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (purchaseReturn is null)
        {
            return (null, Array.Empty<PurchaseReturnItem>());
        }

        // 明细行按插入顺序（明细 Id 为顺序 Guid，与 AddRange 顺序一致）
        var items = await _dbContext.PurchaseReturnItems.AsNoTracking()
            .Where(i => i.ReturnId == id)
            .OrderBy(i => i.Id)
            .ToListAsync(cancellationToken);

        return (purchaseReturn, items);
    }

    /// <inheritdoc />
    public async Task AddAsync(PurchaseReturn purchaseReturn, IReadOnlyList<PurchaseReturnItem> items, CancellationToken cancellationToken = default)
    {
        _dbContext.PurchaseReturns.Add(purchaseReturn);
        _dbContext.PurchaseReturnItems.AddRange(items);
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
        return _dbContext.PurchaseReturns
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
        return _dbContext.PurchaseReturns
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
        // 序号 = 当天同前缀已有单号数 + 1；唯一索引兜底并发冲突（Handler 重试，见 design.md §3.1）
        var dateSegment = returnDate.UtcDateTime.ToString("yyyyMMdd");
        var pattern = $"{prefix}{dateSegment}";
        var count = await _dbContext.PurchaseReturns.CountAsync(r => r.ReturnNo.StartsWith(pattern), cancellationToken);
        return $"{pattern}{(count + 1):D4}";
    }
}
