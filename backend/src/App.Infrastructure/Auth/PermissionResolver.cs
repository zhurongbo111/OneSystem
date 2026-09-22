using App.Core;
using App.Core.Abstractions;
using App.Core.Auth;

using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Auth;

/// <summary>
/// 权限解析器的 EF Core 实现：用户 → 角色 → 权限点。
/// Scoped 生命周期 + 实例内缓存：同一请求多次权限校验只查一次库；
/// 权限不进 JWT，角色变更后下一次请求即生效，无需重新登录。
/// </summary>
public sealed class PermissionResolver : IPermissionResolver
{
    private readonly AppDbContext _dbContext;
    private IReadOnlySet<string>? _cache;

    /// <summary>
    /// 初始化权限解析器
    /// </summary>
    public PermissionResolver(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IReadOnlySet<string>> GetPermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (_cache is not null)
        {
            return _cache;
        }

        var roleNames = await GetRoleNamesAsync(userId, cancellationToken);

        // 超级管理员不逐点存储：命中即返回全量权限点（specs/028-erp-rbac/design.md §3.2）
        if (roleNames.Contains(BuiltinRoles.SuperAdmin, StringComparer.Ordinal))
        {
            _cache = new HashSet<string>(Permissions.All, StringComparer.Ordinal);
            return _cache;
        }

        var keys = await _dbContext.UserRoles.AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .Join(
                _dbContext.RolePermissions.AsNoTracking(),
                ur => ur.RoleId,
                rp => rp.RoleId,
                (ur, rp) => rp.PermissionKey)
            .ToListAsync(cancellationToken);

        _cache = new HashSet<string>(keys, StringComparer.Ordinal);
        return _cache;
    }

    private Task<List<string>> GetRoleNamesAsync(Guid userId, CancellationToken cancellationToken)
        => _dbContext.UserRoles.AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .Join(
                _dbContext.Roles.AsNoTracking(),
                ur => ur.RoleId,
                r => r.Id,
                (ur, r) => r.Name)
            .ToListAsync(cancellationToken);
}
