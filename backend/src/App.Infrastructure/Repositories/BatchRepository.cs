using App.Core.Abstractions;
using App.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 批次档案仓储的 EF Core 实现（PostgreSQL，040-erp-batch-expiry）。只做数据访问，不做业务判定；
/// 过期 / 近效期判定在 Handler 按固定「今天」计算，本类仅按入参 today 做「近效期或已过期」的筛选。
/// </summary>
public sealed class BatchRepository : IBatchRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化批次档案仓储
    /// </summary>
    public BatchRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task<Batch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Batches.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<Batch?> GetByProductAndBatchNoAsync(
        Guid productId, string batchNo, CancellationToken cancellationToken = default)
        => _dbContext.Batches
            .FirstOrDefaultAsync(
                b => b.ProductId == productId && b.BatchNo == batchNo,
                cancellationToken);

    /// <inheritdoc />
    public Task<bool> ExistsByBatchNoAsync(
        Guid productId, string batchNo, Guid? excludeId, CancellationToken cancellationToken = default)
        => _dbContext.Batches
            .AsNoTracking()
            .AnyAsync(
                b => b.ProductId == productId
                    && b.BatchNo.ToLower() == batchNo.Trim().ToLowerInvariant()
                    && (excludeId == null || b.Id != excludeId.Value),
                cancellationToken);

    /// <inheritdoc />
    public async Task<(IReadOnlyList<BatchListItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        Guid? productId,
        PartnerStatus? status,
        bool? onlyExpiring,
        DateTimeOffset today,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = from b in _dbContext.Batches.AsNoTracking()
                    join p in _dbContext.Products.AsNoTracking() on b.ProductId equals p.Id
                    select new
                    {
                        Batch = b,
                        ProductCode = p.Code,
                        ProductName = p.Name,
                    };

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            // 关键词匹配批次号 / 商品编码 / 商品名称（与搜索框占位文案、design §3 一致；
            // 多字段 OR 模式同 ProductRepository.GetPagedAsync）
            var lower = keyword.Trim().ToLowerInvariant();
            query = query.Where(x => x.Batch.BatchNo.ToLower().Contains(lower)
                || x.ProductCode.ToLower().Contains(lower)
                || x.ProductName.ToLower().Contains(lower));
        }

        if (productId is not null)
        {
            var value = productId.Value;
            query = query.Where(x => x.Batch.ProductId == value);
        }

        if (status is not null)
        {
            var value = status.Value;
            query = query.Where(x => x.Batch.Status == value);
        }

        if (onlyExpiring == true)
        {
            // 近效期或已过期：到期日非空且 ≤ 今天 + NearExpiryDays（含已过期，040 §0 判定窗口）
            var windowEnd = today.AddDays(BatchFieldConstraints.NearExpiryDays);
            query = query.Where(x => x.Batch.ExpiryDate != null && x.Batch.ExpiryDate <= windowEnd);
        }

        var total = await query.CountAsync(cancellationToken);

        // 联查库存合计：该批次所有仓批次行 Σ Quantity（无库存行为 0）
        var items = await query
            .OrderByDescending(x => x.Batch.CreatedAt)
            .ThenByDescending(x => x.Batch.BatchNo)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Batch.Id,
                ProductId = x.Batch.ProductId,
                x.ProductCode,
                x.ProductName,
                BatchNo = x.Batch.BatchNo,
                ProductionDate = x.Batch.ProductionDate,
                ExpiryDate = x.Batch.ExpiryDate,
                Status = x.Batch.Status,
                CreatedAt = x.Batch.CreatedAt,
                Remark = x.Batch.Remark,
                TotalStock = _dbContext.Inventory
                    .AsNoTracking()
                    .Where(i => i.BatchId == x.Batch.Id)
                    .Sum(i => (int?)i.Quantity) ?? 0,
            })
            .Select(x => new BatchListItem
            {
                Id = x.Id,
                ProductId = x.ProductId,
                ProductCode = x.ProductCode,
                ProductName = x.ProductName,
                BatchNo = x.BatchNo,
                ProductionDate = x.ProductionDate,
                ExpiryDate = x.ExpiryDate,
                Status = x.Status,
                TotalStock = x.TotalStock,
                // 批次只有一个到期日，最早到期日即其本身（保留字段供批次维度视图扩展）
                EarliestExpiryDate = x.ExpiryDate,
                CreatedAt = x.CreatedAt,
                Remark = x.Remark,
            })
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BatchPickItem>> GetPickListAsync(
        Guid productId, Guid warehouseId, DateTimeOffset today, CancellationToken cancellationToken = default)
    {
        // 开单批次下拉（040 §3.1）：该商品在该仓「有库存或未过期」的启用批次；
        // 已过期的批次仅在仍有库存时保留（出库类单据由前端禁用选择，入库类仍可用）
        var rows = await _dbContext.Batches
            .AsNoTracking()
            .Where(b => b.ProductId == productId
                && b.Status == PartnerStatus.Enabled
                && (b.ExpiryDate == null
                    || b.ExpiryDate >= today
                    || _dbContext.Inventory
                        .AsNoTracking()
                        .Any(i => i.BatchId == b.Id && i.WarehouseId == warehouseId && i.Quantity > 0)))
            .Select(b => new
            {
                BatchId = b.Id,
                b.BatchNo,
                b.ProductionDate,
                b.ExpiryDate,
                b.Status,
                AvailableQuantity = _dbContext.Inventory
                    .AsNoTracking()
                    .Where(i => i.BatchId == b.Id && i.WarehouseId == warehouseId)
                    .Sum(i => (int?)i.Quantity) ?? 0,
            })
            .ToListAsync(cancellationToken);

        return rows
            .OrderBy(r => r.ExpiryDate == null)
            .ThenBy(r => r.ExpiryDate)
            .ThenBy(r => r.BatchNo)
            .Select(r => new BatchPickItem
            {
                BatchId = r.BatchId,
                BatchNo = r.BatchNo,
                ProductionDate = r.ProductionDate,
                ExpiryDate = r.ExpiryDate,
                Status = r.Status,
                AvailableQuantity = r.AvailableQuantity,
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task AddAsync(Batch batch, CancellationToken cancellationToken = default)
    {
        _dbContext.Batches.Add(batch);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Batch batch, CancellationToken cancellationToken = default)
    {
        _dbContext.Batches.Update(batch);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
