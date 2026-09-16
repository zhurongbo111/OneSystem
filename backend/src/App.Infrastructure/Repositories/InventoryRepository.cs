using App.Core.Abstractions;
using App.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 库存台账仓储的 EF Core 实现（PostgreSQL）。只做数据访问，不做业务判定。
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
    public async Task AddAsync(Inventory inventory, CancellationToken cancellationToken = default)
    {
        _dbContext.Inventory.Add(inventory);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> GetQuantityAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var quantity = await _dbContext.Inventory
            .AsNoTracking()
            .Where(i => i.ProductId == productId)
            .Select(i => i.Quantity)
            .FirstOrDefaultAsync(cancellationToken);

        return quantity;
    }

    /// <inheritdoc />
    public Task IncrementAsync(Guid productId, int delta, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return _dbContext.Inventory
            .Where(i => i.ProductId == productId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.Quantity, i => i.Quantity + delta)
                .SetProperty(i => i.UpdatedAt, now),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> TryDecrementAsync(Guid productId, int amount, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        // 条件更新：判断（Quantity >= amount）与扣减在同一条 SQL 内原子完成，并发下最多一个请求扣减成功（防超卖）
        var affected = await _dbContext.Inventory
            .Where(i => i.ProductId == productId && i.Quantity >= amount)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.Quantity, i => i.Quantity - amount)
                .SetProperty(i => i.UpdatedAt, now),
            cancellationToken);

        return affected >= 1;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<InventoryItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        Guid? categoryId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // 仅启用商品；左连接 Inventory 带出库存（无库存行时按 0 计）
        var query = from p in _dbContext.Products.AsNoTracking()
                    where p.Status == ProductStatus.Enabled
                    join c in _dbContext.Categories.AsNoTracking() on p.CategoryId equals c.Id
                    join i in _dbContext.Inventory.AsNoTracking() on p.Id equals i.ProductId into iGroup
                    from i in iGroup.DefaultIfEmpty()
                    select new
                    {
                        Product = p,
                        CategoryName = c.Name,
                        StockQuantity = i == null ? 0 : i.Quantity,
                        UpdatedAt = (DateTimeOffset?)(i == null ? null : i.UpdatedAt),
                    };

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lower = keyword.Trim().ToLowerInvariant();
            query = query.Where(x => x.Product.Code.ToLower().Contains(lower) || x.Product.Name.ToLower().Contains(lower));
        }

        if (categoryId is not null)
        {
            var value = categoryId.Value;
            query = query.Where(x => x.Product.CategoryId == value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.Product.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new InventoryItem
            {
                ProductId = x.Product.Id,
                Code = x.Product.Code,
                Name = x.Product.Name,
                CategoryName = x.CategoryName,
                Unit = x.Product.Unit,
                StockQuantity = x.StockQuantity,
                SafetyStock = x.Product.SafetyStock,
                UpdatedAt = x.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        return (items, total);
    }
}
