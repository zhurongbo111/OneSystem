using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 销售单仓储的 EF Core 实现（PostgreSQL）。只做数据访问，不做业务判定。
/// 审计字段统一由 Handler 经 ICurrentUser 获取后随方法参数 / 实体传入，仓储不感知当前用户。
/// 跨表一致性：主表 + 明细在仓储内一次 SaveChanges；跨仓储写（库存扣减 + 单据）由 Handler
/// 用 IUnitOfWork 包成同一 PostgreSQL 事务（见 erp-sale design.md §3.4）。
/// </summary>
public sealed class SalesOrderRepository : ISalesOrderRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化销售单仓储
    /// </summary>
    public SalesOrderRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<SalesOrderListItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        Guid? partnerId,
        DateTimeOffset? start,
        DateTimeOffset? end,
        OrderSettlementStatus? settlement,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.SalesOrders.AsNoTracking().AsQueryable();

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

        if (settlement is not null)
        {
            var value = settlement.Value;
            query = query.Where(o => o.SettlementStatus == value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new SalesOrderListItem
            {
                Id = o.Id,
                OrderNo = o.OrderNo,
                PartnerId = o.PartnerId,
                PartnerName = o.PartnerName,
                OrderDate = o.OrderDate,
                TotalAmount = o.TotalAmount,
                SettlementStatus = o.SettlementStatus,
                Status = o.Status,
                CreatedAt = o.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    /// <inheritdoc />
    public async Task<SalesOrderDetail?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.SalesOrders.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (order is null)
        {
            return null;
        }

        // 明细行按插入顺序（明细 Id 为顺序 Guid，与 AddRange 顺序一致）
        var items = await _dbContext.SalesOrderItems.AsNoTracking()
            .Where(i => i.OrderId == id)
            .OrderBy(i => i.Id)
            .Select(i => new SalesOrderDetailItem
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Unit = i.Unit,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                Subtotal = i.Subtotal,
            })
            .ToListAsync(cancellationToken);

        return new SalesOrderDetail
        {
            Id = order.Id,
            OrderNo = order.OrderNo,
            PartnerId = order.PartnerId,
            PartnerName = order.PartnerName,
            OrderDate = order.OrderDate,
            TotalAmount = order.TotalAmount,
            SettlementStatus = order.SettlementStatus,
            Status = order.Status,
            Remark = order.Remark,
            CreatedBy = order.CreatedBy,
            CreatedAt = order.CreatedAt,
            Items = items,
        };
    }

    /// <inheritdoc />
    public async Task AddAsync(SalesOrder order, IReadOnlyList<SalesOrderItem> items, CancellationToken cancellationToken = default)
    {
        _dbContext.SalesOrders.Add(order);
        _dbContext.SalesOrderItems.AddRange(items);
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
        return _dbContext.SalesOrders
            .Where(o => o.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.SettlementStatus, settlement)
                .SetProperty(o => o.UpdatedAt, now)
                .SetProperty(o => o.UpdatedBy, operatorId),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task UpdateStatusAsync(Guid id, OrderStatus status, Guid? operatorId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return _dbContext.SalesOrders
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
        var count = await _dbContext.SalesOrders.CountAsync(o => o.OrderNo.StartsWith(pattern), cancellationToken);
        return $"{pattern}{(count + 1):D4}";
    }

}
