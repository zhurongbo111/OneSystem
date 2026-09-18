using App.Core.Abstractions;
using App.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 库存变动流水仓储的 EF Core 实现（PostgreSQL）。只做数据访问，不做业务判定。
/// </summary>
public sealed class StockMovementRepository : IStockMovementRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化库存变动流水仓储
    /// </summary>
    public StockMovementRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task AppendAsync(StockMovement movement, CancellationToken cancellationToken = default)
    {
        _dbContext.StockMovements.Add(movement);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<StockMovementItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        Guid? productId,
        StockMovementType? type,
        DateTimeOffset? start,
        DateTimeOffset? end,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // 左连接 Products 带出编码 / 名称 / 单位（商品停用不影响历史流水展示）；
        // 左连接 Users 带出操作人姓名（CreatedBy 为空或无匹配用户时为 null）
        var query = from m in _dbContext.StockMovements.AsNoTracking()
                    join p in _dbContext.Products.AsNoTracking() on m.ProductId equals p.Id
                    join u in _dbContext.Users.AsNoTracking() on m.CreatedBy equals u.Id into uGroup
                    from u in uGroup.DefaultIfEmpty()
                    select new
                    {
                        m,
                        p,
                        CreatedByName = (string?)(u == null ? null : u.DisplayName),
                    };

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lower = keyword.Trim().ToLowerInvariant();
            query = query.Where(x => x.m.SourceNo != null && x.m.SourceNo.ToLower().Contains(lower));
        }

        if (productId is not null)
        {
            var value = productId.Value;
            query = query.Where(x => x.m.ProductId == value);
        }

        if (type is not null)
        {
            var value = type.Value;
            query = query.Where(x => x.m.MovementType == value);
        }

        if (start is not null)
        {
            var value = start.Value;
            query = query.Where(x => x.m.CreatedAt >= value);
        }

        if (end is not null)
        {
            var value = end.Value;
            query = query.Where(x => x.m.CreatedAt <= value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new StockMovementItem
            {
                Id = x.m.Id,
                ProductId = x.m.ProductId,
                ProductCode = x.p.Code,
                ProductName = x.p.Name,
                Unit = x.p.Unit,
                MovementType = x.m.MovementType,
                Quantity = x.m.Quantity,
                UnitCost = x.m.UnitCost,
                TotalCost = x.m.TotalCost,
                SourceNo = x.m.SourceNo,
                Remark = x.m.Remark,
                CreatedAt = x.m.CreatedAt,
                CreatedByName = x.CreatedByName,
            })
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    /// <inheritdoc />
    public async Task<int> SumQuantityAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.StockMovements
            .AsNoTracking()
            .Where(m => m.ProductId == productId)
            .SumAsync(m => (int?)m.Quantity, cancellationToken) ?? 0;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<Guid>> GetProductIdsWithMovementsAsync(
        IReadOnlyList<Guid> productIds, CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        return await _dbContext.StockMovements
            .AsNoTracking()
            .Where(m => productIds.Contains(m.ProductId))
            .Select(m => m.ProductId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<decimal?> GetMovementUnitCostAsync(
        Guid sourceId,
        Guid productId,
        StockMovementType type,
        CancellationToken cancellationToken = default)
    {
        // 冲销类还原成本：取同一来源单据 + 商品 + 指定类型流水的成本单价（无匹配返回 null，由调用方兜底）
        return await _dbContext.StockMovements
            .AsNoTracking()
            .Where(m => m.SourceId == sourceId && m.ProductId == productId && m.MovementType == type)
            .Select(m => (decimal?)m.UnitCost)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StockMovementCostRow>> GetAllForCostAsync(
        Guid? productId,
        DateTimeOffset? start,
        DateTimeOffset? end,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.StockMovements.AsNoTracking().AsQueryable();

        if (productId is not null)
        {
            var value = productId.Value;
            query = query.Where(m => m.ProductId == value);
        }

        if (start is not null)
        {
            var value = start.Value;
            query = query.Where(m => m.CreatedAt >= value);
        }

        if (end is not null)
        {
            var value = end.Value;
            query = query.Where(m => m.CreatedAt < value);
        }

        var movements = await query
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .Select(m => new StockMovementCostRow
            {
                Id = m.Id,
                ProductId = m.ProductId,
                MovementType = m.MovementType,
                Quantity = m.Quantity,
                SourceId = m.SourceId,
                SourceNo = m.SourceNo,
                CreatedAt = m.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        if (movements.Count == 0)
        {
            return movements;
        }

        // 关联单价按「单据 id + 商品」两趟取数后在内存组装：
        // 直接 join 明细表会因「同一单据同一商品多行」产生笛卡尔放大，导致重算重复计流水
        var purchaseSourceIds = movements
            .Where(m => m.MovementType == StockMovementType.PurchaseInbound && m.SourceId is not null)
            .Select(m => m.SourceId!.Value)
            .Distinct()
            .ToList();

        var purchasePriceMap = new Dictionary<(Guid, Guid), decimal>();
        if (purchaseSourceIds.Count > 0)
        {
            var receiptItems = await _dbContext.PurchaseReceiptItems.AsNoTracking()
                .Where(i => purchaseSourceIds.Contains(i.ReceiptId))
                .Select(i => new { i.ReceiptId, i.ProductId, i.UnitPrice })
                .ToListAsync(cancellationToken);

            foreach (var group in receiptItems.GroupBy(i => (i.ReceiptId, i.ProductId)))
            {
                purchasePriceMap[group.Key] = group.First().UnitPrice;
            }
        }

        var initialSourceIds = movements
            .Where(m => m.MovementType == StockMovementType.InitialStock && m.SourceId is not null)
            .Select(m => m.SourceId!.Value)
            .Distinct()
            .ToList();

        var initialCostMap = new Dictionary<(Guid, Guid), decimal>();
        if (initialSourceIds.Count > 0)
        {
            var takeItems = await _dbContext.StockTakeItems.AsNoTracking()
                .Where(i => initialSourceIds.Contains(i.StockTakeId))
                .Select(i => new { i.StockTakeId, i.ProductId, i.UnitCost })
                .ToListAsync(cancellationToken);

            foreach (var group in takeItems.GroupBy(i => (i.StockTakeId, i.ProductId)))
            {
                initialCostMap[group.Key] = group.First().UnitCost;
            }
        }

        return movements
            .Select(m => m with
            {
                PurchaseUnitPrice = m.SourceId is not null
                    && purchasePriceMap.TryGetValue((m.SourceId.Value, m.ProductId), out var price)
                    ? price
                    : null,
                InitialUnitCost = m.SourceId is not null
                    && initialCostMap.TryGetValue((m.SourceId.Value, m.ProductId), out var cost)
                    ? cost
                    : null,
            })
            .ToList();
    }

    /// <inheritdoc />
    public Task UpdateCostAsync(
        Guid id, decimal unitCost, decimal totalCost, CancellationToken cancellationToken = default)
        => _dbContext.StockMovements
            .Where(m => m.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.UnitCost, unitCost)
                .SetProperty(m => m.TotalCost, totalCost),
            cancellationToken);
}
