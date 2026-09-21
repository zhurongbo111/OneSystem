using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 采购单仓储的 EF Core 实现（PostgreSQL）。只做数据访问，不做业务判定。
/// 审计字段统一由 Handler 经 ICurrentUser 获取后随方法参数 / 实体传入，仓储不感知当前用户。
/// </summary>
public sealed class PurchaseReceiptRepository : IPurchaseReceiptRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化采购单仓储
    /// </summary>
    public PurchaseReceiptRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<(PurchaseReceipt Order, int TotalQuantity)> Items, int Total)> GetPagedAsync(
        string? keyword,
        Guid? partnerId,
        Guid? orderId,
        DateTimeOffset? start,
        DateTimeOffset? end,
        SettlementState? settlementState,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.PurchaseReceipts.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lower = keyword.Trim().ToLowerInvariant();
            query = query.Where(o => o.ReceiptNo.ToLower().Contains(lower) || o.PartnerName.ToLower().Contains(lower));
        }

        if (partnerId is not null)
        {
            var value = partnerId.Value;
            query = query.Where(o => o.PartnerId == value);
        }

        // 关联订单筛选：订单详情的「关联入库单」列表与跟单场景
        if (orderId is not null)
        {
            var value = orderId.Value;
            query = query.Where(o => o.OrderId == value);
        }

        // 日期范围对 OrderDate 闭区间比较（timestamptz 语义，不做时区归一化）
        if (start is not null)
        {
            var s = start.Value;
            query = query.Where(o => o.OrderDate >= s);
        }

        if (end is not null)
        {
            var e = end.Value;
            query = query.Where(o => o.OrderDate <= e);
        }

        if (settlementState is not null)
        {
            // 结算状态为推导值：按已结金额与总额比较过滤（design.md §0 / §2.3）
            var state = settlementState.Value;
            query = state switch
            {
                SettlementState.Unsettled => query.Where(o => o.SettledAmount <= 0),
                SettlementState.PartiallySettled => query.Where(o => o.SettledAmount > 0 && o.SettledAmount < o.TotalAmount),
                _ => query.Where(o => o.SettledAmount >= o.TotalAmount),
            };
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // 数量合计：本页单据的明细数量按单据聚合（单次查询，避免逐单往返；订单详情「关联入库单」跟单用）
        var ids = items.Select(o => o.Id).ToList();
        var quantities = await _dbContext.PurchaseReceiptItems.AsNoTracking()
            .Where(i => ids.Contains(i.ReceiptId))
            .GroupBy(i => i.ReceiptId)
            .Select(g => new { ReceiptId = g.Key, Quantity = g.Sum(i => i.Quantity) })
            .ToListAsync(cancellationToken);
        var quantityMap = quantities.ToDictionary(x => x.ReceiptId, x => x.Quantity);

        var rows = items
            .Select(o => (Order: o, TotalQuantity: quantityMap.TryGetValue(o.Id, out var quantity) ? quantity : 0))
            .ToList();

        return (rows, total);
    }

    /// <inheritdoc />
    public async Task<(PurchaseReceipt? Order, IReadOnlyList<PurchaseReceiptItem> Items)> GetDetailAsync(Guid id, bool includeItems = true, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.PurchaseReceipts.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (order is null)
        {
            return (null, Array.Empty<PurchaseReceiptItem>());
        }

        // 只取主表（核销校验等只用主表字段的场景）：不发起明细查询，Items 恒为空集合
        if (!includeItems)
        {
            return (order, Array.Empty<PurchaseReceiptItem>());
        }

        // 明细行按插入顺序（明细 Id 为顺序 Guid，与 AddRange 顺序一致）
        var items = await _dbContext.PurchaseReceiptItems.AsNoTracking()
            .Where(i => i.ReceiptId == id)
            .OrderBy(i => i.Id)
            .ToListAsync(cancellationToken);

        return (order, items);
    }

    /// <inheritdoc />
    public async Task AddAsync(PurchaseReceipt order, IReadOnlyList<PurchaseReceiptItem> items, CancellationToken cancellationToken = default)
    {
        _dbContext.PurchaseReceipts.Add(order);
        _dbContext.PurchaseReceiptItems.AddRange(items);
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
    public Task AddSettledAmountAsync(Guid id, decimal delta, Guid? operatorId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return _dbContext.PurchaseReceipts
            .Where(o => o.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.SettledAmount, o => o.SettledAmount + delta)
                .SetProperty(o => o.UpdatedAt, now)
                .SetProperty(o => o.UpdatedBy, operatorId),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task UpdateStatusAsync(Guid id, OrderStatus status, Guid? operatorId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return _dbContext.PurchaseReceipts
            .Where(o => o.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.Status, status)
                .SetProperty(o => o.UpdatedAt, now)
                .SetProperty(o => o.UpdatedBy, operatorId),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> GenerateOrderNoAsync(string prefix, DateTimeOffset orderDate, CancellationToken cancellationToken = default)
    {
        // 序号 = 当天同前缀已有单号数 + 1；唯一索引兜底并发冲突（Handler 重试，见 design.md §3.6）
        var dateSegment = orderDate.UtcDateTime.ToString("yyyyMMdd");
        var pattern = $"{prefix}{dateSegment}";
        var count = await _dbContext.PurchaseReceipts.CountAsync(o => o.ReceiptNo.StartsWith(pattern), cancellationToken);
        return $"{pattern}{(count + 1):D4}";
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PurchaseReceiptItem>> GetItemsByOrderIdsAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken = default)
    {
        if (orderIds.Count == 0)
        {
            return Array.Empty<PurchaseReceiptItem>();
        }

        // 导出用批量取明细（一次查询避免逐单 N+1）；按明细 Id 升序即插入顺序
        return await _dbContext.PurchaseReceiptItems.AsNoTracking()
            .Where(i => orderIds.Contains(i.ReceiptId))
            .OrderBy(i => i.Id)
            .ToListAsync(cancellationToken);
    }
}
