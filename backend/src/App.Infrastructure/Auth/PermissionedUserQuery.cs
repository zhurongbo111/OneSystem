using App.Core;
using App.Core.Abstractions;
using App.Core.Entities;

using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Auth;

/// <summary>
/// 「按权限点反查接收人」的 EF Core 实现（041-erp-stock-alert）：复用 `028` 的权限数据
/// （Users ⋈ UserRoles ⋈ RolePermissions），返回持有指定权限点的**启用**用户 id。
/// 超级管理员不逐点存储权限点（028 §3.2），命中内置角色 <see cref="BuiltinRoles.SuperAdmin"/> 的用户视为具备全部权限。
/// </summary>
public sealed class PermissionedUserQuery : IPermissionedUserQuery
{
    private readonly AppDbContext _dbContext;

    /// <summary>
    /// 初始化接收人反查查询
    /// </summary>
    public PermissionedUserQuery(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> GetEnabledUserIdsByPermissionAsync(
        string permissionKey, CancellationToken cancellationToken = default)
    {
        // 显式持有权限点的启用用户
        var permissionedUserIds = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Status == UserStatus.Enabled)
            .Join(
                _dbContext.UserRoles.AsNoTracking(),
                u => u.Id,
                ur => ur.UserId,
                (u, ur) => new { u.Id, ur.RoleId })
            .Join(
                _dbContext.RolePermissions.AsNoTracking(),
                x => x.RoleId,
                rp => rp.RoleId,
                (x, rp) => new { x.Id, rp.PermissionKey })
            .Where(x => x.PermissionKey == permissionKey)
            .Select(x => x.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

        // 超级管理员：角色命中即视为具备全部权限点（与 PermissionResolver 同口径）
        var superAdminUserIds = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Status == UserStatus.Enabled)
            .Join(
                _dbContext.UserRoles.AsNoTracking(),
                u => u.Id,
                ur => ur.UserId,
                (u, ur) => new { u.Id, ur.RoleId })
            .Join(
                _dbContext.Roles.AsNoTracking(),
                x => x.RoleId,
                r => r.Id,
                (x, r) => new { x.Id, r.Name })
            .Where(x => x.Name == BuiltinRoles.SuperAdmin)
            .Select(x => x.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

        return permissionedUserIds
            .Union(superAdminUserIds)
            .ToList();
    }
}
