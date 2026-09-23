using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 报价单仓储的 EF Core 实现（PostgreSQL）。只做数据访问，不做业务判定。
/// 报价单不触碰库存、库存流水与收付款；审计字段统一由 Handler 经 ICurrentUser 获取后随方法参数 / 实体传入。
/// </summary>
public sealed class QuotationRepository : IQuotationRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化报价单仓储
    /// </summary>
    public QuotationRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<QuotationListItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        QuotationStatus? status,
        DateTimeOffset? start,
        DateTimeOffset? end,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Quotations.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lower = keyword.Trim().ToLowerInvariant();
            query = query.Where(q => q.QuotationNo.ToLower().Contains(lower) || q.PartnerName.ToLower().Contains(lower));
        }

        if (status is not null)
        {
            var value = status.Value;
            query = query.Where(q => q.Status == value);
        }

        // 日期范围对 QuotationDate 闭区间比较（timestamptz 语义，不做时区归一化）
        if (start is not null)
        {
            var s = start.Value;
            query = query.Where(q => q.QuotationDate >= s);
        }

        if (end is not null)
        {
            var e = end.Value;
            query = query.Where(q => q.QuotationDate <= e);
        }

        var total = await query.CountAsync(cancellationToken);
        var quotations = await query
            .OrderByDescending(q => q.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // 明细行数：本页报价单一次聚合取回（避免逐单往返）
        var quotationIds = quotations.Select(q => q.Id).ToList();
        var itemCounts = await _dbContext.QuotationItems.AsNoTracking()
            .Where(i => quotationIds.Contains(i.QuotationId))
            .GroupBy(i => i.QuotationId)
            .Select(g => new { QuotationId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var countMap = itemCounts.ToDictionary(x => x.QuotationId, x => x.Count);

        var items = quotations
            .Select(q => new QuotationListItem
            {
                Quotation = q,
                ItemCount = countMap.TryGetValue(q.Id, out var count) ? count : 0,
            })
            .ToList();

        return (items, total);
    }

    /// <inheritdoc />
    public async Task<(Quotation? Quotation, IReadOnlyList<QuotationItem> Items)> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var quotation = await _dbContext.Quotations.AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

        if (quotation is null)
        {
            return (null, Array.Empty<QuotationItem>());
        }

        // 明细行按插入顺序（明细 Id 为顺序 Guid，与 AddRange 顺序一致）
        var items = await _dbContext.QuotationItems.AsNoTracking()
            .Where(i => i.QuotationId == id)
            .OrderBy(i => i.Id)
            .ToListAsync(cancellationToken);

        return (quotation, items);
    }

    /// <inheritdoc />
    public async Task AddAsync(Quotation quotation, IReadOnlyList<QuotationItem> items, CancellationToken cancellationToken = default)
    {
        _dbContext.Quotations.Add(quotation);
        _dbContext.QuotationItems.AddRange(items);
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
    public async Task UpdateAsync(Quotation quotation, IReadOnlyList<QuotationItem> items, CancellationToken cancellationToken = default)
    {
        // 主表按实体全量更新（Handler 传入的是查出的原实体 + 可改字段变更；单号 / 创建审计字段值不变）
        _dbContext.Quotations.Update(quotation);

        // 明细全量替换（仅草稿可改）
        var oldItems = await _dbContext.QuotationItems
            .Where(i => i.QuotationId == quotation.Id)
            .ToListAsync(cancellationToken);
        _dbContext.QuotationItems.RemoveRange(oldItems);
        _dbContext.QuotationItems.AddRange(items);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task UpdateStatusAsync(
        Guid id,
        QuotationStatus status,
        Guid? convertedOrderId,
        string? convertedOrderNo,
        Guid? operatorId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return _dbContext.Quotations
            .Where(q => q.Id == id)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(q => q.Status, status)
                    .SetProperty(q => q.ConvertedOrderId, convertedOrderId)
                    .SetProperty(q => q.ConvertedOrderNo, convertedOrderNo)
                    .SetProperty(q => q.UpdatedAt, now)
                    .SetProperty(q => q.UpdatedBy, operatorId),
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> GenerateNoAsync(string prefix, DateTimeOffset quotationDate, CancellationToken cancellationToken = default)
    {
        // 序号 = 当天同前缀已有报价单数 + 1；唯一索引兜底并发冲突（Handler 重试）
        var dateSegment = quotationDate.UtcDateTime.ToString("yyyyMMdd");
        var pattern = $"{prefix}{dateSegment}";
        var count = await _dbContext.Quotations.CountAsync(q => q.QuotationNo.StartsWith(pattern), cancellationToken);
        return $"{pattern}{(count + 1):D4}";
    }
}
