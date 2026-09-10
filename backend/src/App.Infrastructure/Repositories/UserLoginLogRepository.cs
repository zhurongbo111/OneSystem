using App.Core.Abstractions;
using App.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 登录日志仓储的 EF Core 实现（只追加 + 分页查询，不提供修改 / 删除）
/// </summary>
public sealed class UserLoginLogRepository : IUserLoginLogRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化登录日志仓储
    /// </summary>
    public UserLoginLogRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task AddAsync(UserLoginLog log, CancellationToken cancellationToken = default)
    {
        _dbContext.UserLoginLogs.Add(log);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<UserLoginLog> Items, int Total)> GetPagedAsync(
        string? username,
        DateTimeOffset? startTime,
        DateTimeOffset? endTime,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.UserLoginLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(username))
        {
            var lower = username.Trim().ToLowerInvariant();
            query = query.Where(l => l.Username.ToLower().Contains(lower));
        }

        if (startTime is not null)
        {
            var from = startTime.Value;
            query = query.Where(l => l.LoginAt >= from);
        }

        if (endTime is not null)
        {
            var to = endTime.Value;
            query = query.Where(l => l.LoginAt <= to);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(l => l.LoginAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }
}
