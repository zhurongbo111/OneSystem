namespace App.Core.Entities;

/// <summary>
/// 角色相关字段约束的**单一来源**：EF 实体配置（<c>HasMaxLength</c>）与各 <c>RequestValidator</c> 均引用本类常量，
/// 保证"格式校验"与"数据库约束"始终一致（后端规则 §5.3）。
/// </summary>
public static class RoleFieldConstraints
{
    /// <summary>角色名最短长度</summary>
    public const int NameMinLength = 2;

    /// <summary>角色名最大长度（对齐 Roles.Name varchar(20)）</summary>
    public const int NameMaxLength = 20;

    /// <summary>备注最大长度（复用单据备注口径，对齐 Roles.Remark varchar(200)）</summary>
    public const int RemarkMaxLength = OrderFieldConstraints.RemarkMaxLength;

    /// <summary>权限点 key 最大长度（对齐 RolePermissions.PermissionKey varchar(60)）</summary>
    public const int PermissionKeyMaxLength = 60;

    /// <summary>单个角色可勾选权限点数量上限</summary>
    public const int PermissionsPerRoleMaxCount = 200;

    /// <summary>单个用户可绑定角色数量上限</summary>
    public const int RolesPerUserMaxCount = 20;
}
