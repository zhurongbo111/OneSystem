using App.Core.Abstractions;
using App.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 库存预警只读查询仓储的 EF Core 实现（PostgreSQL，041-erp-stock-alert）。
/// 判定口径唯一来源见 specs/041-erp-stock-alert/design.md §0：低库存 = 按「商品 × 仓」汇总
/// <c>Σ Quantity &lt; MAX(SafetyStock)</c> 且 <c>MAX(SafetyStock) &gt; 0</c> 且商品启用；
/// 近效期 / 过期 = 该「商品 × 仓 × 批次」库存 &gt; 0 且到期日在窗口内 / 已过。
/// 聚合统一走「SQL 分组投影 + 内存按 id 拼装名称」，不在已分组子查询上再套外层聚合（后端规则 §5.4）。
/// </summary>
public sealed class StockAlertQueryRepository : IStockAlertQueryRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化库存预警只读查询仓储
    /// </summary>
    public StockAlertQueryRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StockAlertSignal>> GetLowStockSignalsAsync(
        int maxCount, CancellationToken cancellationToken = default)
    {
        // 单层分组 + HAVING 语义（WHERE SafetyStock > 0 && Quantity < SafetyStock），整体可翻译为 SQL
        var summary = await _dbContext.Inventory
            .AsNoTracking()
            .GroupBy(i => new { i.ProductId, i.WarehouseId })
            .Select(g => new
            {
                g.Key.ProductId,
                g.Key.WarehouseId,
                Quantity = g.Sum(x => x.Quantity),
                SafetyStock = g.Max(x => x.SafetyStock),
            })
            .Where(x => x.SafetyStock > 0 && x.Quantity < x.SafetyStock)
            .OrderBy(x => x.Quantity - x.SafetyStock)
            .ThenBy(x => x.ProductId)
            .Take(maxCount)
            .ToListAsync(cancellationToken);

        if (summary.Count == 0)
        {
            return [];
        }

        var productIds = summary.Select(x => x.ProductId).Distinct().ToList();
        var warehouseIds = summary.Select(x => x.WarehouseId).Distinct().ToList();

        // 商品编码 / 名称（仅启用商品参与告警，038 §0 口径）与仓库名称一次取回，避免逐行 N+1
        var products = await _dbContext.Products
            .AsNoTracking()
            .Where(p => productIds.Contains(p.Id) && p.Status == ProductStatus.Enabled)
            .Select(p => new { p.Id, p.Code, p.Name })
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var warehouses = await _dbContext.Warehouses
            .AsNoTracking()
            .Where(w => warehouseIds.Contains(w.Id))
            .Select(w => new { w.Id, w.Name })
            .ToDictionaryAsync(w => w.Id, cancellationToken);

        return summary
            .Where(x => products.ContainsKey(x.ProductId) && warehouses.ContainsKey(x.WarehouseId))
            .Select(x => new StockAlertSignal
            {
                ProductId = x.ProductId,
                ProductCode = products[x.ProductId].Code,
                ProductName = products[x.ProductId].Name,
                WarehouseId = x.WarehouseId,
                WarehouseName = warehouses[x.WarehouseId].Name,
                Quantity = x.Quantity,
                SafetyStock = x.SafetyStock,
            })
            .ToList();
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<StockAlertSignal>> GetExpiringBatchSignalsAsync(
        DateOnly today, int nearDays, int maxCount, CancellationToken cancellationToken = default)
        => QueryBatchSignalsAsync(today, nearDays, expiredOnly: false, maxCount, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<StockAlertSignal>> GetExpiredBatchSignalsAsync(
        DateOnly today, int maxCount, CancellationToken cancellationToken = default)
        => QueryBatchSignalsAsync(today, nearDays: 0, expiredOnly: true, maxCount, cancellationToken);

    /// <summary>
    /// 批次信号查询：近效期（<c>今天 &lt;= ExpiryDate &lt;= 今天 + nearDays</c>）与已过期（<c>ExpiryDate &lt; 今天</c>）共用，
    /// 均要求该「商品 × 仓 × 批次」库存 &gt; 0（040 §0 口径：库存为 0 的过期批次不告警）。
    /// </summary>
    private async Task<IReadOnlyList<StockAlertSignal>> QueryBatchSignalsAsync(
        DateOnly today, int nearDays, bool expiredOnly, int maxCount, CancellationToken cancellationToken)
    {
        // 到期日为 UTC 午夜，按 timestamptz 直接比较（与 040 既有实现同口径）
        var todayUtc = new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var windowEnd = todayUtc.AddDays(nearDays);

        var query = from i in _dbContext.Inventory.AsNoTracking()
                    where i.BatchId != null && i.Quantity > 0
                    join b in _dbContext.Batches.AsNoTracking() on i.BatchId equals (Guid?)b.Id
                    where b.ExpiryDate != null
                    join p in _dbContext.Products.AsNoTracking() on i.ProductId equals p.Id
                    join w in _dbContext.Warehouses.AsNoTracking() on i.WarehouseId equals w.Id
                    select new
                    {
                        i.ProductId,
                        i.WarehouseId,
                        i.Quantity,
                        ProductCode = p.Code,
                        ProductName = p.Name,
                        WarehouseName = w.Name,
                        BatchId = b.Id,
                        b.BatchNo,
                        b.ExpiryDate,
                    };

        query = expiredOnly
            ? query.Where(x => x.ExpiryDate < todayUtc)
            : query.Where(x => x.ExpiryDate >= todayUtc && x.ExpiryDate <= windowEnd);

        var rows = await query
            .OrderBy(x => x.ExpiryDate)
            .ThenBy(x => x.BatchNo)
            .Take(maxCount)
            .ToListAsync(cancellationToken);

        return rows
            .Select(x => new StockAlertSignal
            {
                ProductId = x.ProductId,
                ProductCode = x.ProductCode,
                ProductName = x.ProductName,
                WarehouseId = x.WarehouseId,
                WarehouseName = x.WarehouseName,
                Quantity = x.Quantity,
                SafetyStock = 0,
                BatchId = x.BatchId,
                BatchNo = x.BatchNo,
                ExpiryDate = x.ExpiryDate,
            })
            .ToList();
    }
}
