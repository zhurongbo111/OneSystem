using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 商机仓储的 EF Core 实现（PostgreSQL）。只做数据访问，不做业务判定。
/// 商机不触碰库存、库存流水与收付款；客户名称取商机自身的快照列，负责人姓名按 id 批量取回；
/// 审计字段统一由 Handler 经 ICurrentUser 获取后随实体传入。
/// </summary>
public sealed class OpportunityRepository : IOpportunityRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化商机仓储
    /// </summary>
    public OpportunityRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<OpportunityListItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        OpportunityStage? stage,
        Guid? partnerId,
        Guid? ownerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Opportunities.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lower = keyword.Trim().ToLowerInvariant();
            query = query.Where(o => o.OpportunityNo.ToLower().Contains(lower) || o.Name.ToLower().Contains(lower));
        }

        if (stage is not null)
        {
            var value = stage.Value;
            query = query.Where(o => o.Stage == value);
        }

        if (partnerId is not null)
        {
            var value = partnerId.Value;
            query = query.Where(o => o.PartnerId == value);
        }

        if (ownerId is not null)
        {
            var value = ownerId.Value;
            query = query.Where(o => o.OwnerId == value);
        }

        var total = await query.CountAsync(cancellationToken);
        var opportunities = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // 负责人姓名：本页商机一次批量取回（避免逐行往返）
        var ownerNames = await GetOwnerNamesAsync(
            opportunities.Where(o => o.OwnerId is not null).Select(o => o.OwnerId!.Value), cancellationToken);

        var items = opportunities
            .Select(o => new OpportunityListItem
            {
                Opportunity = o,
                OwnerName = o.OwnerId is not null && ownerNames.TryGetValue(o.OwnerId.Value, out var name) ? name : null,
            })
            .ToList();

        return (items, total);
    }

    /// <inheritdoc />
    public async Task<OpportunityDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var opportunity = await _dbContext.Opportunities.AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (opportunity is null)
        {
            return null;
        }

        string? ownerName = null;
        if (opportunity.OwnerId is not null)
        {
            var ownerId = opportunity.OwnerId.Value;
            ownerName = await _dbContext.Employees.AsNoTracking()
                .Where(e => e.Id == ownerId)
                .Select(e => e.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new OpportunityDetail { Opportunity = opportunity, OwnerName = ownerName };
    }

    /// <inheritdoc />
    public async Task AddAsync(Opportunity opportunity, CancellationToken cancellationToken = default)
    {
        _dbContext.Opportunities.Add(opportunity);
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
    public async Task UpdateAsync(Opportunity opportunity, CancellationToken cancellationToken = default)
    {
        _dbContext.Opportunities.Update(opportunity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> GenerateNoAsync(string prefix, DateTimeOffset opportunityDate, CancellationToken cancellationToken = default)
    {
        // 序号 = 当天同前缀已有商机数 + 1；唯一索引兜底并发冲突（Handler 重试）
        var dateSegment = opportunityDate.UtcDateTime.ToString("yyyyMMdd");
        var pattern = $"{prefix}{dateSegment}";
        var count = await _dbContext.Opportunities.CountAsync(o => o.OpportunityNo.StartsWith(pattern), cancellationToken);
        return $"{pattern}{(count + 1):D4}";
    }

    /// <summary>按员工 id 批量取姓名（id 去重，一次查询）</summary>
    private async Task<Dictionary<Guid, string>> GetOwnerNamesAsync(
        IEnumerable<Guid> ownerIds, CancellationToken cancellationToken)
    {
        var ids = ownerIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await _dbContext.Employees.AsNoTracking()
            .Where(e => ids.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Name, cancellationToken);
    }
}
