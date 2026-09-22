using App.Core.Abstractions;
using App.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Repositories;

/// <summary>
/// 用户角色关联仓储的 EF Core 实现（PostgreSQL）。
/// 集合型从属表：更新一律「先删后插」全量替换，不做差异计算。
/// </summary>
public sealed class UserRoleRepository : IUserRoleRepository
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化用户角色关联仓储
    /// </summary>
    public UserRoleRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> GetRoleIdsByUserAsync(Guid userId, CancellationToken cancellationToken = default)
        => await _dbContext.UserRoles.AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<UserRoleItem>>> GetRolesByUserIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<UserRoleItem>>();
        }

        var rows = await _dbContext.UserRoles.AsNoTracking()
            .Where(ur => userIds.Contains(ur.UserId))
            .Join(
                _dbContext.Roles.AsNoTracking(),
                ur => ur.RoleId,
                r => r.Id,
                (ur, r) => new { ur.UserId, RoleId = r.Id, RoleName = r.Name })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.UserId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<UserRoleItem>)g
                    .Select(x => new UserRoleItem { Id = x.RoleId, Name = x.RoleName })
                    .OrderBy(x => x.Name)
                    .ToList());
    }

    /// <inheritdoc />
    public async Task ReplaceUserRolesAsync(
        Guid userId,
        IReadOnlyCollection<Guid> roleIds,
        CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.UserRoles
            .Where(ur => ur.UserId == userId)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
        {
            _dbContext.UserRoles.RemoveRange(existing);
        }

        var rows = roleIds.Select(roleId => new UserRole { UserId = userId, RoleId = roleId }).ToList();
        if (rows.Count > 0)
        {
            _dbContext.UserRoles.AddRange(rows);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<int> CountByRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
        => _dbContext.UserRoles.AsNoTracking().CountAsync(ur => ur.RoleId == roleId, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, int>> CountByRoleIdsAsync(
        IReadOnlyCollection<Guid> roleIds,
        CancellationToken cancellationToken = default)
    {
        if (roleIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var rows = await _dbContext.UserRoles.AsNoTracking()
            .Where(ur => roleIds.Contains(ur.RoleId))
            .GroupBy(ur => ur.RoleId)
            .Select(g => new { RoleId = g.Key, UserCount = g.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(x => x.RoleId, x => x.UserCount);
    }
}
