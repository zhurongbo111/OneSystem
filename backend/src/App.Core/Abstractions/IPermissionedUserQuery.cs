namespace App.Core.Abstractions;

/// <summary>
/// 「按权限点反查接收人」只读查询接口（实现见 App.Infrastructure；041-erp-stock-alert）。
/// 复用 `028` 的权限数据（Users ⋈ UserRoles ⋈ RolePermissions）：告警接收人 = 持有指定权限点的**启用**用户，
/// 无需再做订阅配置（范围外）。单独成接口以便扫描器单测注入替身。
/// </summary>
public interface IPermissionedUserQuery
{
    /// <summary>
    /// 取持有指定权限点的启用用户 id 列表（去重）
    /// </summary>
    /// <param name="permissionKey">权限点 key（见 <c>App.Core.Auth.Permissions</c>）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<IReadOnlyList<Guid>> GetEnabledUserIdsByPermissionAsync(
        string permissionKey, CancellationToken cancellationToken = default);
}
