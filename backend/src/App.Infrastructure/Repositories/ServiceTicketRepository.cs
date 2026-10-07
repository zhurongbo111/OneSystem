using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 服务工单仓储的 EF Core 实现（PostgreSQL）。只做数据访问，不做业务判定。
/// 工单不触碰库存、库存流水与收付款；负责人姓名按 id 批量取回（避免逐行联查）；
/// 审计字段统一由 Handler 经 ICurrentUser 获取后随实体传入。
/// </summary>
public sealed class ServiceTicketRepository : IServiceTicketRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化服务工单仓储
    /// </summary>
    public ServiceTicketRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<ServiceTicketListItem> Items, int Total)> GetPagedAsync(
        string? keyword,
        TicketStatus? status,
        TicketPriority? priority,
        Guid? ownerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ServiceTickets.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lower = keyword.Trim().ToLowerInvariant();
            query = query.Where(t =>
                t.TicketNo.ToLower().Contains(lower)
                || t.PartnerName.ToLower().Contains(lower)
                || t.Title.ToLower().Contains(lower));
        }

        if (status is not null)
        {
            var value = status.Value;
            query = query.Where(t => t.Status == value);
        }

        if (priority is not null)
        {
            var value = priority.Value;
            query = query.Where(t => t.Priority == value);
        }

        if (ownerId is not null)
        {
            var value = ownerId.Value;
            query = query.Where(t => t.OwnerId == value);
        }

        var total = await query.CountAsync(cancellationToken);
        var tickets = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // 负责人姓名：本页工单一次批量取回（避免逐行往返）
        var ownerNames = await GetOwnerNamesAsync(
            tickets.Where(t => t.OwnerId is not null).Select(t => t.OwnerId!.Value), cancellationToken);

        var items = tickets
            .Select(t => new ServiceTicketListItem
            {
                Ticket = t,
                OwnerName = t.OwnerId is not null && ownerNames.TryGetValue(t.OwnerId.Value, out var name) ? name : null,
            })
            .ToList();

        return (items, total);
    }

    /// <inheritdoc />
    public async Task<ServiceTicketDetail?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var ticket = await _dbContext.ServiceTickets.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (ticket is null)
        {
            return null;
        }

        string? ownerName = null;
        if (ticket.OwnerId is not null)
        {
            var ownerId = ticket.OwnerId.Value;
            ownerName = await _dbContext.Employees.AsNoTracking()
                .Where(e => e.Id == ownerId)
                .Select(e => e.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new ServiceTicketDetail { Ticket = ticket, OwnerName = ownerName };
    }

    /// <inheritdoc />
    public async Task AddAsync(ServiceTicket ticket, CancellationToken cancellationToken = default)
    {
        _dbContext.ServiceTickets.Add(ticket);
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
    public async Task UpdateAsync(ServiceTicket ticket, CancellationToken cancellationToken = default)
    {
        _dbContext.ServiceTickets.Update(ticket);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<string> GenerateNoAsync(string prefix, DateTimeOffset ticketDate, CancellationToken cancellationToken = default)
    {
        // 序号 = 当天同前缀已有工单数 + 1；唯一索引兜底并发冲突（Handler 重试）
        var dateSegment = ticketDate.UtcDateTime.ToString("yyyyMMdd");
        var pattern = $"{prefix}{dateSegment}";
        var count = await _dbContext.ServiceTickets.CountAsync(t => t.TicketNo.StartsWith(pattern), cancellationToken);
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
