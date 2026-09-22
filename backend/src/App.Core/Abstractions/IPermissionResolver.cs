using App.Core.Auth;

namespace App.Core.Abstractions;

/// <summary>
/// 权限解析器（实现见 App.Infrastructure.Auth.PermissionResolver）：按用户求其全部权限点（多角色取并集）。
/// 实现为 Scoped 生命周期且在单请求内缓存——同一请求多次校验只查一次库；
/// 权限不进 JWT，角色变更后下一次请求即生效，无需重新登录。
/// </summary>
public interface IPermissionResolver
{
    /// <summary>
    /// 求指定用户的权限点集合；内置超级管理员返回 <see cref="Permissions.All"/>，无角色返回空集
    /// </summary>
    /// <param name="userId">用户 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlySet<string>> GetPermissionsAsync(Guid userId, CancellationToken cancellationToken = default);
}
