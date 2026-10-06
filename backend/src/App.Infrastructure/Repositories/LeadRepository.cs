using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 线索仓储的 EF Core 实现（PostgreSQL）。只做数据访问，不做业务判定。
/// 线索不触碰库存、库存流水与收付款；负责人姓名按 id 批量取回（避免逐行联查）；
/// 审计字段统一由 Handler 经 ICurrentUser 获取后随实体传入。
/// </summary>
public sealed class LeadRepository : ILeadRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化线索仓储
    /// </summary>
    public LeadRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<LeadListItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        LeadSource? source,
        LeadStatus? status,
        Guid? ownerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Leads.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lower = keyword.Trim().ToLowerInvariant();
            query = query.Where(l =>
                l.LeadNo.ToLower().Contains(lower)
                || l.Name.ToLower().Contains(lower)
                || (l.Contact != null && l.Contact.ToLower().Contains(lower))
                || (l.Phone != null && l.Phone.Contains(lower)));
        }

        if (source is not null)
        {
            var value = source.Value;
            query = query.Where(l => l.Source == value);
        }

        if (status is not null)
        {
            var value = status.Value;
            query = query.Where(l => l.Status == value);
        }

        if (ownerId is not null)
        {
            var value = ownerId.Value;
            query = query.Where(l => l.OwnerId == value);
        }

        var total = await query.CountAsync(cancellationToken);
        var leads = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // 负责人姓名：本页线索一次批量取回（避免逐行往返）
        var ownerNames = await GetOwnerNamesAsync(
            leads.Where(l => l.OwnerId is not null).Select(l => l.OwnerId!.Value), cancellationToken);

        var items = leads
            .Select(l => new LeadListItem
            {
                Lead = l,
                OwnerName = l.OwnerId is not null && ownerNames.TryGetValue(l.OwnerId.Value, out var name) ? name : null,
            })
            .ToList();

        return (items, total);
    }

    /// <inheritdoc />
    public async Task<LeadDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var lead = await _dbContext.Leads.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

        if (lead is null)
        {
            return null;
        }

        string? ownerName = null;
        if (lead.OwnerId is not null)
        {
            var ownerId = lead.OwnerId.Value;
            ownerName = await _dbContext.Employees.AsNoTracking()
                .Where(e => e.Id == ownerId)
                .Select(e => e.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new LeadDetail { Lead = lead, OwnerName = ownerName };
    }

    /// <inheritdoc />
    public async Task AddAsync(Lead lead, CancellationToken cancellationToken = default)
    {
        _dbContext.Leads.Add(lead);
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
    public async Task UpdateAsync(Lead lead, CancellationToken cancellationToken = default)
    {
        _dbContext.Leads.Update(lead);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> GenerateNoAsync(string prefix, DateTimeOffset leadDate, CancellationToken cancellationToken = default)
    {
        // 序号 = 当天同前缀已有线索数 + 1；唯一索引兜底并发冲突（Handler 重试）
        var dateSegment = leadDate.UtcDateTime.ToString("yyyyMMdd");
        var pattern = $"{prefix}{dateSegment}";
        var count = await _dbContext.Leads.CountAsync(l => l.LeadNo.StartsWith(pattern), cancellationToken);
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
