using App.Core.Abstractions;
using App.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 库存台账仓储的 EF Core 实现（PostgreSQL）。只做数据访问，不做业务判定。
/// 库存按「商品 × 仓库 × 批次」定位（040 维度升级；batchId 为 null 的行即 038 的非批次行）；
/// 原子增减用 ExecuteUpdate 表达（无裸 SQL）：更新语句在数据库行锁内完成，并发下无需显式行锁 / 乐观版本号。
/// </summary>
public sealed class InventoryRepository : IInventoryRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化库存台账仓储
    /// </summary>
    public InventoryRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task EnsureRowAsync(
        Guid productId, Guid warehouseId, Guid? batchId, int safetyStock, CancellationToken cancellationToken = default)
    {
        var exists = await _dbContext.Inventory
            .AsNoTracking()
            .AnyAsync(i => i.ProductId == productId && i.WarehouseId == warehouseId && i.BatchId == batchId, cancellationToken);
        if (exists)
        {
            return;
        }

        _dbContext.Inventory.Add(new Inventory
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            WarehouseId = warehouseId,
            BatchId = batchId,
            Quantity = 0,
            SafetyStock = safetyStock,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> GetQuantityAsync(
        Guid productId, Guid warehouseId, Guid? batchId, CancellationToken cancellationToken = default)
        => _dbContext.Inventory
            .AsNoTracking()
            .Where(i => i.ProductId == productId && i.WarehouseId == warehouseId && i.BatchId == batchId)
            .Select(i => i.Quantity)
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<int> GetTotalQuantityAsync(Guid productId, CancellationToken cancellationToken = default)
        => await _dbContext.Inventory
            .AsNoTracking()
            .Where(i => i.ProductId == productId)
            .SumAsync(i => (int?)i.Quantity, cancellationToken) ?? 0;

    /// <inheritdoc />
    public async Task<int> GetWarehouseQuantityAsync(
        Guid productId, Guid warehouseId, CancellationToken cancellationToken = default)
        => await _dbContext.Inventory
            .AsNoTracking()
            .Where(i => i.ProductId == productId && i.WarehouseId == warehouseId)
            .SumAsync(i => (int?)i.Quantity, cancellationToken) ?? 0;

    /// <inheritdoc />
    public async Task IncrementAsync(
        Guid productId, Guid warehouseId, Guid? batchId, int delta, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var affected = await _dbContext.Inventory
            .Where(i => i.ProductId == productId && i.WarehouseId == warehouseId && i.BatchId == batchId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.Quantity, i => i.Quantity + delta)
                .SetProperty(i => i.UpdatedAt, now),
            cancellationToken);

        if (affected > 0 || delta <= 0)
        {
            // 行已存在（已累加）；或扣减类增量遇缺行（数据异常，不新建负库存行）
            return;
        }

        // 仓后建 / 批次后建行场景：该行尚无库存行且为入库，先建行（安全库存取商品档案阈值）再落数量
        var safetyStock = await _dbContext.Products
            .AsNoTracking()
            .Where(p => p.Id == productId)
            .Select(p => p.SafetyStock)
            .FirstOrDefaultAsync(cancellationToken);

        _dbContext.Inventory.Add(new Inventory
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            WarehouseId = warehouseId,
            BatchId = batchId,
            Quantity = delta,
            SafetyStock = safetyStock,
            UpdatedAt = now,
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> TryDecrementAsync(
        Guid productId, Guid warehouseId, Guid? batchId, int amount, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        // 条件更新：判断（Quantity >= amount）与扣减在同一条 SQL 内原子完成，并发下最多一个请求扣减成功（防超卖）
        var affected = await _dbContext.Inventory
            .Where(i => i.ProductId == productId && i.WarehouseId == warehouseId && i.BatchId == batchId && i.Quantity >= amount)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.Quantity, i => i.Quantity - amount)
                .SetProperty(i => i.UpdatedAt, now),
            cancellationToken);

        return affected >= 1;
    }

    /// <inheritdoc />
    public Task<int> SetQuantityAsync(
        Guid productId, Guid warehouseId, Guid? batchId, int quantity, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        // 原子设定为指定值（ExecuteUpdate 行锁内完成）；该行无库存行时受影响行数为 0
        return _dbContext.Inventory
            .Where(i => i.ProductId == productId && i.WarehouseId == warehouseId && i.BatchId == batchId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.Quantity, quantity)
                .SetProperty(i => i.UpdatedAt, now),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, int>> GetQuantitiesAsync(
        Guid warehouseId, IReadOnlyList<Guid> productIds, CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        // 不带批次：按批次行 Σ 汇总（040 后非批次商品仍只有一行，行为与 038 一致）
        var rows = await _dbContext.Inventory
            .AsNoTracking()
            .Where(i => i.WarehouseId == warehouseId && productIds.Contains(i.ProductId))
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(i => i.Quantity) })
            .ToListAsync(cancellationToken);

        var result = new Dictionary<Guid, int>(productIds.Count);
        foreach (var id in productIds)
        {
            // 该仓无库存行的商品按 0 计
            result[id] = rows.FirstOrDefault(r => r.ProductId == id)?.Quantity ?? 0;
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, int>> GetBatchQuantitiesAsync(
        Guid warehouseId, Guid productId, CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.Inventory
            .AsNoTracking()
            .Where(i => i.WarehouseId == warehouseId && i.ProductId == productId && i.BatchId != null)
            .Select(i => new { i.BatchId, i.Quantity })
            .ToListAsync(cancellationToken);

        return rows
            .Where(r => r.BatchId != null)
            .ToDictionary(r => r.BatchId!.Value, r => r.Quantity);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<InventoryItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        Guid? categoryId,
        Guid? warehouseId,
        Guid? batchId,
        string? batchNo,
        bool expandBatch,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // 展开批次视图：按「商品 × 仓 × 批次行」一行；汇总视图：按「商品 × 仓」一行（批次行 Σ 数量、MAX 安全库存，040 §0）
        var query = from p in _dbContext.Products.AsNoTracking()
                    where p.Status == ProductStatus.Enabled
                    join c in _dbContext.Categories.AsNoTracking() on p.CategoryId equals c.Id
                    join i in _dbContext.Inventory.AsNoTracking() on p.Id equals i.ProductId into iGroup
                    from i in iGroup.DefaultIfEmpty()
                    join w in _dbContext.Warehouses.AsNoTracking() on i.WarehouseId equals w.Id
                    join b in _dbContext.Batches.AsNoTracking() on i.BatchId equals b.Id into bGroup
                    from b in bGroup.DefaultIfEmpty()
                    select new InventoryRow
                    {
                        ProductId = p.Id,
                        ProductCode = p.Code,
                        ProductName = p.Name,
                        CategoryId = p.CategoryId,
                        Unit = p.Unit,
                        CreatedAt = p.CreatedAt,
                        CreatedBy = p.CreatedBy,
                        CategoryName = c.Name,
                        WarehouseId = i.WarehouseId,
                        WarehouseName = w.Name,
                        BatchId = i.BatchId,
                        BatchNo = b!.BatchNo,
                        ExpiryDate = b!.ExpiryDate,
                        StockQuantity = i.Quantity,
                        SafetyStock = i.SafetyStock,
                        UpdatedAt = (DateTimeOffset?)i.UpdatedAt,
                    };

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lower = keyword.Trim().ToLowerInvariant();
            query = query.Where(x => x.ProductCode.ToLower().Contains(lower) || x.ProductName.ToLower().Contains(lower));
        }

        if (categoryId is not null)
        {
            var value = categoryId.Value;
            query = query.Where(x => x.CategoryId == value);
        }

        if (warehouseId is not null)
        {
            var value = warehouseId.Value;
            query = query.Where(x => x.WarehouseId == value);
        }

        if (batchId is not null)
        {
            var value = batchId.Value;
            query = query.Where(x => x.BatchId == value);
        }

        if (!string.IsNullOrWhiteSpace(batchNo))
        {
            // 批次号模糊筛选（大小写不敏感；汇总视图下非批次行 BatchNo 为 null，自然被过滤掉）
            var value = batchNo.Trim().ToLowerInvariant();
            query = query.Where(x => x.BatchNo != null && x.BatchNo.ToLower().Contains(value));
        }

        if (!expandBatch)
        {
            // 汇总视图：批次行折叠为「商品 × 仓」一行；安全库存判定口径 = MAX(SafetyStock)（040 §0）
            query = query
                .GroupBy(x => new { x.ProductId, x.ProductCode, x.ProductName, x.CategoryId, x.Unit, x.CreatedAt, x.CreatedBy, x.CategoryName, x.WarehouseId, x.WarehouseName })
                .Select(g => new InventoryRow
                {
                    ProductId = g.Key.ProductId,
                    ProductCode = g.Key.ProductCode,
                    ProductName = g.Key.ProductName,
                    CategoryId = g.Key.CategoryId,
                    Unit = g.Key.Unit,
                    CreatedAt = g.Key.CreatedAt,
                    CreatedBy = g.Key.CreatedBy,
                    CategoryName = g.Key.CategoryName,
                    WarehouseId = g.Key.WarehouseId,
                    WarehouseName = g.Key.WarehouseName,
                    BatchId = null,
                    BatchNo = null,
                    ExpiryDate = null,
                    StockQuantity = g.Sum(x => x.StockQuantity),
                    SafetyStock = g.Max(x => x.SafetyStock),
                    UpdatedAt = g.Max(x => x.UpdatedAt),
                });
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.ProductCode)
            .ThenBy(x => x.WarehouseName)
            .ThenBy(x => x.BatchNo)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new InventoryItem
            {
                ProductId = x.ProductId,
                Code = x.ProductCode,
                Name = x.ProductName,
                CategoryName = x.CategoryName,
                Unit = x.Unit,
                WarehouseId = x.WarehouseId,
                WarehouseName = x.WarehouseName,
                BatchId = x.BatchId,
                BatchNo = x.BatchNo,
                ExpiryDate = x.ExpiryDate,
                StockQuantity = x.StockQuantity,
                SafetyStock = x.SafetyStock,
                UpdatedAt = x.UpdatedAt,
                CreatedAt = x.CreatedAt,
                CreatedBy = x.CreatedBy,
            })
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    /// <inheritdoc />
    public Task<int> UpdateSafetyStockAsync(
        Guid productId, Guid warehouseId, int safetyStock, CancellationToken cancellationToken = default)
        => _dbContext.Inventory
            .Where(i => i.ProductId == productId && i.WarehouseId == warehouseId)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.SafetyStock, safetyStock), cancellationToken);

    /// <inheritdoc />
    public Task<decimal> GetAverageCostAsync(
        Guid productId, Guid warehouseId, Guid? batchId, CancellationToken cancellationToken = default)
    {
        // 该行无库存行时按 0 处理（与 GetQuantityAsync 的无行即 0 语义一致）
        return _dbContext.Inventory
            .AsNoTracking()
            .Where(i => i.ProductId == productId && i.WarehouseId == warehouseId && i.BatchId == batchId)
            .Select(i => i.AverageCost)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task ApplyInboundCostAsync(
        Guid productId,
        Guid warehouseId,
        Guid? batchId,
        int quantity,
        decimal unitCost,
        CancellationToken cancellationToken = default)
    {
        // 入库金额在应用侧按财务惯例四舍五入（AwayFromZero），不依赖数据库舍入语义；
        // 调用方已先完成数量增加，此处读到的 Quantity 即结存新值（先加数量再加金额）
        var delta = Math.Round(quantity * unitCost, 4, MidpointRounding.AwayFromZero);

        return _dbContext.Inventory
            .Where(i => i.ProductId == productId && i.WarehouseId == warehouseId && i.BatchId == batchId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.CostAmount, i => i.CostAmount + delta)
                // 均价 = 结存金额 ÷ 结存数量（派生值）；数量为 0 时保留最后均价，避免除零与均价丢失
                .SetProperty(i => i.AverageCost, i => i.Quantity == 0
                    ? i.AverageCost
                    : (i.CostAmount + delta) / i.Quantity),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task ApplyOutboundCostAsync(
        Guid productId,
        Guid warehouseId,
        Guid? batchId,
        decimal totalCost,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Inventory
            .Where(i => i.ProductId == productId && i.WarehouseId == warehouseId && i.BatchId == batchId)
            .ExecuteUpdateAsync(s => s
                // 数量归零时成本额归 0（消除长期小额尾差）；均价不变 —— 按均价出库不改变均值
                .SetProperty(i => i.CostAmount, i => i.Quantity == 0 ? 0m : i.CostAmount - totalCost),
            cancellationToken);
    }

    /// <inheritdoc />
    public Task SetCostAsync(
        Guid productId,
        Guid warehouseId,
        Guid? batchId,
        decimal costAmount,
        decimal averageCost,
        CancellationToken cancellationToken = default)
        => _dbContext.Inventory
            .Where(i => i.ProductId == productId && i.WarehouseId == warehouseId && i.BatchId == batchId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.CostAmount, costAmount)
                .SetProperty(i => i.AverageCost, averageCost),
            cancellationToken);

    /// <summary>
    /// 库存分页查询的读行（040）：展开视图一行 = 商品 × 仓 × 批次行；汇总视图由 GroupBy 折叠为 商品 × 仓 一行。
    /// </summary>
    private sealed record InventoryRow
    {
        /// <summary>商品 id</summary>
        public required Guid ProductId { get; init; }

        /// <summary>商品编码</summary>
        public required string ProductCode { get; init; }

        /// <summary>商品名称</summary>
        public required string ProductName { get; init; }

        /// <summary>分类 id</summary>
        public required Guid CategoryId { get; init; }

        /// <summary>计量单位</summary>
        public required string Unit { get; init; }

        /// <summary>商品创建时间</summary>
        public required DateTimeOffset CreatedAt { get; init; }

        /// <summary>商品创建人</summary>
        public Guid? CreatedBy { get; init; }

        /// <summary>分类名称</summary>
        public required string CategoryName { get; init; }

        /// <summary>仓库 id</summary>
        public required Guid WarehouseId { get; init; }

        /// <summary>仓库名称</summary>
        public required string WarehouseName { get; init; }

        /// <summary>批次 id（汇总视图 / 非批次行为 null）</summary>
        public Guid? BatchId { get; init; }

        /// <summary>批次号（汇总视图 / 非批次行为 null）</summary>
        public string? BatchNo { get; init; }

        /// <summary>到期日（汇总视图 / 非批次行为 null）</summary>
        public DateTimeOffset? ExpiryDate { get; init; }

        /// <summary>库存数量（汇总视图 = Σ 批次行）</summary>
        public required int StockQuantity { get; init; }

        /// <summary>安全库存（汇总视图 = MAX 批次行）</summary>
        public required int SafetyStock { get; init; }

        /// <summary>最近变动时间（汇总视图 = MAX 批次行）</summary>
        public DateTimeOffset? UpdatedAt { get; init; }
    }
}
