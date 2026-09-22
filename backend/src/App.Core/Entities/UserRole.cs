namespace App.Core.Entities;

/// <summary>
/// 用户角色关联实体（对应 PostgreSQL 表 UserRoles）。
/// 用户与角色多对多；无审计字段，更新同样采用「先删后插」全量替换。
/// </summary>
public sealed class UserRole
{
    /// <summary>用户 ID，复合主键之一</summary>
    public Guid UserId { get; set; }

    /// <summary>角色 ID，复合主键之一</summary>
    public Guid RoleId { get; set; }
}
