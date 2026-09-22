namespace App.Core.Entities;

/// <summary>
/// 部门实体（对应 PostgreSQL 表 Departments）。
/// 树形结构用 <see cref="ParentId"/> 自引用维护（<c>NULL</c> = 顶级）；防环在 Handler 内沿父链上溯判定。
/// </summary>
public sealed class Department
{
    /// <summary>部门 ID</summary>
    public Guid Id { get; set; }

    /// <summary>部门编码，全局唯一</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>部门名称（同一上级下唯一，由应用层按大小写不敏感判定）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>上级部门 id（<c>null</c> 表示顶级部门）</summary>
    public Guid? ParentId { get; set; }

    /// <summary>同级排序（升序展示）</summary>
    public int SortOrder { get; set; }

    /// <summary>部门状态（启用 / 停用）</summary>
    public DepartmentStatus Status { get; set; } = DepartmentStatus.Enabled;

    /// <summary>备注，可空</summary>
    public string? Remark { get; set; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>更新时间</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>创建人用户 id</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>更新人用户 id</summary>
    public Guid? UpdatedBy { get; set; }
}
