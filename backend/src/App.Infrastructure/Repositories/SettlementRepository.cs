using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 收付款单仓储的 EF Core 实现（PostgreSQL）。只做数据访问，不做业务判定。
/// 审计字段统一由 Handler 经 ICurrentUser 获取后随方法参数 / 实体传入，仓储不感知当前用户。
/// </summary>
public sealed class SettlementRepository : ISettlementRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化收付款单仓储
    /// </summary>
    public SettlementRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<Settlement> Items, int Total)> GetPagedAsync(
        string? keyword,
        SettlementType? type,
        Guid? partnerId,
        SettlementMethod? method,
        DateTimeOffset? start,
        DateTimeOffset? end,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Settlements.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lower = keyword.Trim().ToLowerInvariant();
            query = query.Where(s => s.SettlementNo.ToLower().Contains(lower) || s.PartnerName.ToLower().Contains(lower));
        }

        if (type is not null)
        {
            var value = type.Value;
            query = query.Where(s => s.Type == value);
        }

        if (partnerId is not null)
        {
            var value = partnerId.Value;
            query = query.Where(s => s.PartnerId == value);
        }

        if (method is not null)
        {
            var value = method.Value;
            query = query.Where(s => s.Method == value);
        }

        // 日期范围对 SettlementDate 闭区间比较（timestamptz 语义，不做时区归一化）
        if (start is not null)
        {
            var s = start.Value;
            query = query.Where(x => x.SettlementDate >= s);
        }

        if (end is not null)
        {
            var e = end.Value;
            query = query.Where(x => x.SettlementDate <= e);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    /// <inheritdoc />
    public async Task<(Settlement? Settlement, IReadOnlyList<SettlementItem> Items)> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var settlement = await _dbContext.Settlements.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (settlement is null)
        {
            return (null, Array.Empty<SettlementItem>());
        }

        // 核销明细按插入顺序（明细 Id 为顺序 Guid，与 AddRange 顺序一致）
        var items = await _dbContext.SettlementItems.AsNoTracking()
            .Where(i => i.SettlementId == id)
            .OrderBy(i => i.Id)
            .ToListAsync(cancellationToken);

        return (settlement, items);
    }

    /// <inheritdoc />
    public async Task AddAsync(Settlement settlement, IReadOnlyList<SettlementItem> items, CancellationToken cancellationToken = default)
    {
        _dbContext.Settlements.Add(settlement);
        _dbContext.SettlementItems.AddRange(items);
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
    public Task UpdateStatusAsync(Guid id, OrderStatus status, Guid? operatorId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return _dbContext.Settlements
            .Where(s => s.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, status)
                .SetProperty(x => x.UpdatedAt, now)
                .SetProperty(x => x.UpdatedBy, operatorId),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> GenerateSettlementNoAsync(SettlementType type, DateTimeOffset settlementDate, CancellationToken cancellationToken = default)
    {
        // 前缀按类型区分（收款 RC / 付款 PY，见 specs/ROADMAP.md §6.7）；
        // 序号 = 当天同前缀已有单号数 + 1；唯一索引兜底并发冲突（Handler 重试）
        var prefix = type == SettlementType.Receipt ? "RC" : "PY";
        var dateSegment = settlementDate.UtcDateTime.ToString("yyyyMMdd");
        var pattern = $"{prefix}{dateSegment}";
        var count = await _dbContext.Settlements.CountAsync(s => s.SettlementNo.StartsWith(pattern), cancellationToken);
        return $"{pattern}{(count + 1):D4}";
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SettlementItem>> GetItemsBySettlementIdsAsync(IReadOnlyCollection<Guid> settlementIds, CancellationToken cancellationToken = default)
    {
        if (settlementIds.Count == 0)
        {
            return Array.Empty<SettlementItem>();
        }

        // 导出用批量取核销明细（一次查询避免逐单 N+1）；按明细 Id 升序即插入顺序
        return await _dbContext.SettlementItems.AsNoTracking()
            .Where(i => settlementIds.Contains(i.SettlementId))
            .OrderBy(i => i.Id)
            .ToListAsync(cancellationToken);
    }
}
