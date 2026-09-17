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
}
