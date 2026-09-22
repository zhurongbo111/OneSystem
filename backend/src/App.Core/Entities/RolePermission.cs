namespace App.Core.Entities;

/// <summary>
/// 角色权限关联实体（对应 PostgreSQL 表 RolePermissions）。
/// 集合型从属表：无审计字段，保存采用「先删后插」全量替换。
/// </summary>
public sealed class RolePermission
{
    /// <summary>角色 ID，复合主键之一</summary>
    public Guid RoleId { get; set; }

    /// <summary>权限点 key（见 App.Core.Auth.Permissions），复合主键之一</summary>
    public string PermissionKey { get; set; } = string.Empty;
}
