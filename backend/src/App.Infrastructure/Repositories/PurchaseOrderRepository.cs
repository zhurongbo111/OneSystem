using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 采购订单仓储的 EF Core 实现（PostgreSQL）。只做数据访问，不做业务判定。
/// 订单不触碰库存与库存流水；审计字段统一由 Handler 经 ICurrentUser 获取后随方法参数 / 实体传入。
/// </summary>
public sealed class PurchaseOrderRepository : IPurchaseOrderRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化采购订单仓储
    /// </summary>
    public PurchaseOrderRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<(PurchaseOrder Order, int UnfulfilledQuantity)> Items, int Total)> GetPagedAsync(
        string? keyword,
        Guid? partnerId,
        OrderFlowStatus? flowStatus,
        DateTimeOffset? start,
        DateTimeOffset? end,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.PurchaseOrders.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lower = keyword.Trim().ToLowerInvariant();
            query = query.Where(o => o.OrderNo.ToLower().Contains(lower) || o.PartnerName.ToLower().Contains(lower));
        }

        if (partnerId is not null)
        {
            var value = partnerId.Value;
            query = query.Where(o => o.PartnerId == value);
        }

        if (flowStatus is not null)
        {
            var value = flowStatus.Value;
            query = query.Where(o => o.FlowStatus == value);
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

        var total = await query.CountAsync(cancellationToken);
        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // 未收数量：本页订单的明细未执行量按订单聚合（单次查询，避免逐单往返）
        var orderIds = orders.Select(o => o.Id).ToList();
        var unfulfilled = await _dbContext.PurchaseOrderItems.AsNoTracking()
            .Where(i => orderIds.Contains(i.OrderId))
            .GroupBy(i => i.OrderId)
            .Select(g => new { OrderId = g.Key, Quantity = g.Sum(i => i.Quantity - i.FulfilledQuantity) })
            .ToListAsync(cancellationToken);
        var unfulfilledMap = unfulfilled.ToDictionary(x => x.OrderId, x => x.Quantity);

        var items = orders
            .Select(o => (Order: o, UnfulfilledQuantity: unfulfilledMap.TryGetValue(o.Id, out var value) ? value : 0))
            .ToList();

        return (items, total);
    }

    /// <inheritdoc />
    public async Task<(PurchaseOrder? Order, IReadOnlyList<PurchaseOrderItem> Items)> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.PurchaseOrders.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (order is null)
        {
            return (null, Array.Empty<PurchaseOrderItem>());
        }

        // 明细行按插入顺序（明细 Id 为顺序 Guid，与 AddRange 顺序一致）
        var items = await _dbContext.PurchaseOrderItems.AsNoTracking()
            .Where(i => i.OrderId == id)
            .OrderBy(i => i.Id)
            .ToListAsync(cancellationToken);

        return (order, items);
    }

    /// <inheritdoc />
    public Task<(PurchaseOrder? Order, IReadOnlyList<PurchaseOrderItem> Items)> GetLinesAsync(Guid orderId, CancellationToken cancellationToken = default)
        => GetDetailAsync(orderId, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<PurchaseOrder>> GetPicksAsync(Guid partnerId, CancellationToken cancellationToken = default)
        => await _dbContext.PurchaseOrders.AsNoTracking()
            .Where(o => o.PartnerId == partnerId
                && (o.FlowStatus == OrderFlowStatus.Pending || o.FlowStatus == OrderFlowStatus.Partial))
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(PurchaseOrder order, IReadOnlyList<PurchaseOrderItem> items, CancellationToken cancellationToken = default)
    {
        _dbContext.PurchaseOrders.Add(order);
        _dbContext.PurchaseOrderItems.AddRange(items);
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
    public async Task UpdateAsync(PurchaseOrder order, IReadOnlyList<PurchaseOrderItem> items, CancellationToken cancellationToken = default)
    {
        // 主表按实体全量更新（Handler 传入的是查出的原实体 + 可改字段变更；编号 / 创建审计字段值不变）
        _dbContext.PurchaseOrders.Update(order);

        // 明细全量替换（仅「待收货」可改，此时累计执行量恒为 0）
        var oldItems = await _dbContext.PurchaseOrderItems
            .Where(i => i.OrderId == order.Id)
            .ToListAsync(cancellationToken);
        _dbContext.PurchaseOrderItems.RemoveRange(oldItems);
        _dbContext.PurchaseOrderItems.AddRange(items);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task AddFulfilledQuantityAsync(Guid orderItemId, int delta, CancellationToken cancellationToken = default)
        => _dbContext.PurchaseOrderItems
            .Where(i => i.Id == orderItemId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(i => i.FulfilledQuantity, i => i.FulfilledQuantity + delta),
                cancellationToken);

    /// <inheritdoc />
    public Task UpdateFlowStatusAsync(Guid id, OrderFlowStatus status, Guid? operatorId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return _dbContext.PurchaseOrders
            .Where(o => o.Id == id)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(o => o.FlowStatus, status)
                    .SetProperty(o => o.UpdatedAt, now)
                    .SetProperty(o => o.UpdatedBy, operatorId),
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> GenerateOrderNoAsync(string prefix, DateTimeOffset orderDate, CancellationToken cancellationToken = default)
    {
        // 序号 = 当天同前缀已有订单数 + 1；唯一索引兜底并发冲突（Handler 重试，见 design.md §3.6）
        var dateSegment = orderDate.UtcDateTime.ToString("yyyyMMdd");
        var pattern = $"{prefix}{dateSegment}";
        var count = await _dbContext.PurchaseOrders.CountAsync(o => o.OrderNo.StartsWith(pattern), cancellationToken);
        return $"{pattern}{(count + 1):D4}";
    }
}
