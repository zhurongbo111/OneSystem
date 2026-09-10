using App.Core.Abstractions;
using App.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 用户仓储的 EF Core 实现（PostgreSQL）。只做数据访问，不做业务判定。
/// </summary>
public sealed class UserRepository : IUserRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化用户仓储
    /// </summary>
    public UserRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        // 该查询同时服务于详情读取与编辑 / 启停 / 重置密码，需跟踪实体以便后续更新，故不使用 AsNoTracking
        => _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var lower = username.Trim().ToLowerInvariant();
        return _dbContext.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == lower, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> ExistsByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var lower = username.Trim().ToLowerInvariant();
        return _dbContext.Users.AnyAsync(u => u.Username.ToLower() == lower, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> ExistsByEmailAsync(string email, Guid? excludeUserId = null, CancellationToken cancellationToken = default)
    {
        var lower = email.Trim().ToLowerInvariant();
        var query = _dbContext.Users.Where(u => u.Email != null && u.Email.ToLower() == lower);
        if (excludeUserId is not null)
        {
            var excludeId = excludeUserId.Value;
            query = query.Where(u => u.Id != excludeId);
        }

        return query.AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> ExistsByPhoneAsync(string phone, Guid? excludeUserId = null, CancellationToken cancellationToken = default)
    {
        var value = phone.Trim();
        var query = _dbContext.Users.Where(u => u.Phone != null && u.Phone == value);
        if (excludeUserId is not null)
        {
            var excludeId = excludeUserId.Value;
            query = query.Where(u => u.Id != excludeId);
        }

        return query.AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<User> Items, int Total)> GetPagedAsync(
        string? keyword,
        UserStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var lower = keyword.Trim().ToLowerInvariant();
            query = query.Where(u => u.Username.ToLower().Contains(lower) || u.DisplayName.ToLower().Contains(lower));
        }

        if (status is not null)
        {
            var value = status.Value;
            query = query.Where(u => u.Status == value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    /// <inheritdoc />
    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        _dbContext.Users.Update(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateLastLoginAsync(Guid id, DateTimeOffset lastLoginAt, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return;
        }

        user.LastLoginAt = lastLoginAt;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
