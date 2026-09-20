using App.Core.Abstractions;
using App.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 商品仓储的 EF Core 实现（PostgreSQL）。只做数据访问，不做业务判定。
/// 列表 / 详情 / 开单选择的库存列由仓储联查 Inventory 带出，低库存标记由 Handler 计算。
/// </summary>
public sealed class ProductRepository : IProductRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化商品仓储
    /// </summary>
    public ProductRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        // 该查询同时服务于详情读取与编辑 / 启停，需跟踪实体以便后续更新，故不使用 AsNoTracking
        => _dbContext.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<bool> ExistsByCodeAsync(string code, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var lower = code.Trim().ToLowerInvariant();
        var query = _dbContext.Products.Where(p => p.Code.ToLower() == lower);
        if (excludeId is not null)
        {
            var exclude = excludeId.Value;
            query = query.Where(p => p.Id != exclude);
        }

        return query.AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<ProductListItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        Guid? categoryId,
        ProductStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // 联查 Inventory 带出当前库存（无库存行时按 0 计）
        var query = from p in _dbContext.Products.AsNoTracking()
                    join c in _dbContext.Categories.AsNoTracking() on p.CategoryId equals c.Id
                    join i in _dbContext.Inventory.AsNoTracking() on p.Id equals i.ProductId into iGroup
                    from i in iGroup.DefaultIfEmpty()
                    select new
                    {
                        Product = p,
                        CategoryName = c.Name,
                        StockQuantity = i == null ? 0 : i.Quantity,
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

        if (status is not null)
        {
            var value = status.Value;
            query = query.Where(x => x.Product.Status == value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.Product.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ProductListItem
            {
                Id = x.Product.Id,
                Code = x.Product.Code,
                Name = x.Product.Name,
                CategoryId = x.Product.CategoryId,
                CategoryName = x.CategoryName,
                Unit = x.Product.Unit,
                PurchasePrice = x.Product.PurchasePrice,
                SalePrice = x.Product.SalePrice,
                SafetyStock = x.Product.SafetyStock,
                StockQuantity = x.StockQuantity,
                Status = x.Product.Status,
                CreatedAt = x.Product.CreatedAt,
                UpdatedAt = x.Product.UpdatedAt,
                CreatedBy = x.Product.CreatedBy,
            })
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    /// <inheritdoc />
    public Task<ProductDetail?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Products.AsNoTracking()
            .Where(p => p.Id == id)
            .Join(
                _dbContext.Categories.AsNoTracking(),
                p => p.CategoryId,
                c => c.Id,
                (p, c) => new { Product = p, CategoryName = c.Name })
            .GroupJoin(
                _dbContext.Inventory.AsNoTracking(),
                x => x.Product.Id,
                i => i.ProductId,
                (x, iGroup) => new { x.Product, x.CategoryName, Inventory = iGroup.FirstOrDefault() })
            .Select(x => new ProductDetail
            {
                Id = x.Product.Id,
                Code = x.Product.Code,
                Name = x.Product.Name,
                CategoryId = x.Product.CategoryId,
                CategoryName = x.CategoryName,
                Unit = x.Product.Unit,
                PurchasePrice = x.Product.PurchasePrice,
                SalePrice = x.Product.SalePrice,
                SafetyStock = x.Product.SafetyStock,
                // 左连接：库存行缺失时按 0 计，与列表查询行为一致
                StockQuantity = x.Inventory == null ? 0 : x.Inventory.Quantity,
                Status = x.Product.Status,
                Remark = x.Product.Remark,
                CreatedAt = x.Product.CreatedAt,
                UpdatedAt = x.Product.UpdatedAt,
                CreatedBy = x.Product.CreatedBy,
                UpdatedBy = x.Product.UpdatedBy,
            })
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Product product, CancellationToken cancellationToken = default)
    {
        _dbContext.Products.Update(product);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProductPickItem>> GetPickListAsync(CancellationToken cancellationToken = default)
    {
        var items = await _dbContext.Products.AsNoTracking()
            .Where(p => p.Status == ProductStatus.Enabled)
            .Join(
                _dbContext.Inventory.AsNoTracking(),
                p => p.Id,
                i => i.ProductId,
                (p, i) => new ProductPickItem
                {
                    Id = p.Id,
                    Code = p.Code,
                    Name = p.Name,
                    Unit = p.Unit,
                    PurchasePrice = p.PurchasePrice,
                    SalePrice = p.SalePrice,
                    StockQuantity = i.Quantity,
                })
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);

        return items;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, string>> GetCodesByIdsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var rows = await _dbContext.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Code })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(x => x.Id, x => x.Code);
    }
}
