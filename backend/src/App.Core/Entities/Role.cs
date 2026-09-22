namespace App.Core.Entities;

/// <summary>
/// 角色实体（对应 PostgreSQL 表 Roles）。
/// 角色由界面维护（新增 / 编辑 / 删除），权限点集合由界面勾选后全量替换；内置角色不可删除。
/// </summary>
public sealed class Role
{
    /// <summary>角色 ID</summary>
    public Guid Id { get; set; }

    /// <summary>角色名称，唯一（大小写不敏感，由应用层判定）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>备注，可空</summary>
    public string? Remark { get; set; }

    /// <summary>是否内置角色（SuperAdmin / Staff）：不可删除；SuperAdmin 不可编辑</summary>
    public bool IsBuiltin { get; set; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>更新时间</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>创建人用户 id（种子创建时为空）</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>更新人用户 id</summary>
    public Guid? UpdatedBy { get; set; }
}
