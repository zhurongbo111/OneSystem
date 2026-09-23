using App.Core.Abstractions;
using App.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 客户协议价仓储的 EF Core 实现（PostgreSQL）。只做数据访问，不做业务判定
/// （specs/036-erp-partner-price/design.md §3.1）。
/// </summary>
public sealed class PartnerPriceRepository : IPartnerPriceRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化客户协议价仓储
    /// </summary>
    public PartnerPriceRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task<PartnerPrice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        // 该查询同时服务于详情读取与编辑 / 删除，需跟踪实体以便后续更新，故不使用 AsNoTracking
        => _dbContext.PartnerPrices.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<bool> ExistsAsync(Guid partnerId, Guid productId, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.PartnerPrices.Where(p => p.PartnerId == partnerId && p.ProductId == productId);
        if (excludeId is not null)
        {
            var exclude = excludeId.Value;
            query = query.Where(p => p.Id != exclude);
        }

        return query.AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<PartnerPriceListItem> Items, int Total)> GetPagedAsync(
        Guid? partnerId,
        Guid? productId,
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // 列表需展示客户名 / 商品编码 / 名称 / 单位 / 当前销售价，故联查 Partners 与 Products
        var query = from price in _dbContext.PartnerPrices.AsNoTracking()
                    join partner in _dbContext.Partners.AsNoTracking() on price.PartnerId equals partner.Id
                    join product in _dbContext.Products.AsNoTracking() on price.ProductId equals product.Id
                    select new { price, partner, product };

        if (partnerId is not null)
        {
            var value = partnerId.Value;
            query = query.Where(x => x.price.PartnerId == value);
        }

        if (productId is not null)
        {
            var value = productId.Value;
            query = query.Where(x => x.price.ProductId == value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lower = keyword.Trim().ToLowerInvariant();
            query = query.Where(x => x.partner.Name.ToLower().Contains(lower)
                || x.product.Code.ToLower().Contains(lower)
                || x.product.Name.ToLower().Contains(lower));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.price.CreatedAt)
            .ThenBy(x => x.partner.Name)
            .ThenBy(x => x.product.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new PartnerPriceListItem
            {
                Id = x.price.Id,
                PartnerId = x.price.PartnerId,
                PartnerName = x.partner.Name,
                ProductId = x.price.ProductId,
                ProductCode = x.product.Code,
                ProductName = x.product.Name,
                Unit = x.product.Unit,
                Price = x.price.Price,
                SalePrice = x.product.SalePrice,
                Remark = x.price.Remark,
            })
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<EffectivePriceItem>> GetEffectiveAsync(
        Guid partnerId,
        IReadOnlyList<Guid> productIds,
        CancellationToken cancellationToken = default)
    {
        var ids = productIds.Distinct().ToList();

        var prices = await _dbContext.PartnerPrices.AsNoTracking()
            .Where(p => p.PartnerId == partnerId && ids.Contains(p.ProductId))
            .Select(p => new { p.ProductId, p.Price })
            .ToListAsync(cancellationToken);

        var products = await _dbContext.Products.AsNoTracking()
            .Where(p => ids.Contains(p.Id))
            .Select(p => new { p.Id, p.SalePrice })
            .ToListAsync(cancellationToken);

        var priceMap = prices.ToDictionary(p => p.ProductId, p => p.Price);

        // 取价优先级（§0.1）：有协议价即协议价（含与销售价相等的场合），否则商品销售价
        return products.Select(p => new EffectivePriceItem
        {
            ProductId = p.Id,
            UnitPrice = priceMap.TryGetValue(p.Id, out var price) ? price : p.SalePrice,
            Source = priceMap.ContainsKey(p.Id) ? PriceSource.Agreement : PriceSource.Default,
        }).ToList();
    }

    /// <inheritdoc />
    public async Task AddAsync(PartnerPrice partnerPrice, CancellationToken cancellationToken = default)
    {
        _dbContext.PartnerPrices.Add(partnerPrice);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(PartnerPrice partnerPrice, CancellationToken cancellationToken = default)
    {
        _dbContext.PartnerPrices.Update(partnerPrice);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var partnerPrice = await _dbContext.PartnerPrices.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (partnerPrice is null)
        {
            return;
        }

        _dbContext.PartnerPrices.Remove(partnerPrice);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}