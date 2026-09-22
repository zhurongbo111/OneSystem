using App.Core.Auth;

namespace App.Core;

/// <summary>
/// 内置角色名称常量（种子与权限解析共用，避免字符串散落）。
/// 角色行本身由 <c>App.Infrastructure/Persistence/DatabaseInitializer</c> 在启动时幂等创建。
/// </summary>
public static class BuiltinRoles
{
    /// <summary>超级管理员：拥有全部权限点（解析时直接返回 <see cref="Permissions.All"/>，不逐点存储）</summary>
    public const string SuperAdmin = "SuperAdmin";

    /// <summary>普通员工：默认角色，拥有全部业务权限（排除用户 / 角色 / 审计 / 成本重算）</summary>
    public const string Staff = "Staff";
}
