using App.Core.Abstractions;
using App.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 业务操作审计日志仓储的 EF Core 实现（纯追加 + 分页查询，不提供修改 / 删除）。
/// </summary>
public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化审计日志仓储
    /// </summary>
    public AuditLogRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task AddAsync(AuditLog log, CancellationToken cancellationToken = default)
    {
        _dbContext.AuditLogs.Add(log);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<AuditLog> Items, int Total)> GetPagedAsync(
        string? keyword,
        AuditResource? resource,
        AuditAction? action,
        Guid? userId,
        DateTimeOffset? start,
        DateTimeOffset? end,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lower = keyword.Trim().ToLowerInvariant();
            query = query.Where(l =>
                (l.ResourceNo != null && l.ResourceNo.ToLower().Contains(lower))
                || (l.Username != null && l.Username.ToLower().Contains(lower))
                || (l.DisplayName != null && l.DisplayName.ToLower().Contains(lower)));
        }

        if (resource is not null)
        {
            var resourceValue = resource.Value;
            query = query.Where(l => l.Resource == resourceValue);
        }

        if (action is not null)
        {
            var actionValue = action.Value;
            query = query.Where(l => l.Action == actionValue);
        }

        if (userId is not null)
        {
            var operatorId = userId.Value;
            query = query.Where(l => l.UserId == operatorId);
        }

        if (start is not null)
        {
            var from = start.Value;
            query = query.Where(l => l.CreatedAt >= from);
        }

        if (end is not null)
        {
            var to = end.Value;
            query = query.Where(l => l.CreatedAt <= to);
        }

        var total = await query.CountAsync(cancellationToken);

        // 列表不加载 Changes：该列是字段级差异 JSON，体积大且列表页不用
        var items = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new AuditLog
            {
                Id = l.Id,
                UserId = l.UserId,
                Username = l.Username,
                DisplayName = l.DisplayName,
                Resource = l.Resource,
                Action = l.Action,
                ResourceId = l.ResourceId,
                ResourceNo = l.ResourceNo,
                Summary = l.Summary,
                CreatedAt = l.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    /// <inheritdoc />
    public async Task<AuditLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _dbContext.AuditLogs.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
}
