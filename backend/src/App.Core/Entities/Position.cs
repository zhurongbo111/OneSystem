namespace App.Core.Entities;

/// <summary>
/// 岗位实体（对应 PostgreSQL 表 Positions）。
/// 岗位为独立字典，与部门无隶属关系（员工同时挂「部门 + 岗位」）。
/// </summary>
public sealed class Position
{
    /// <summary>岗位 ID</summary>
    public Guid Id { get; set; }

    /// <summary>岗位编码，全局唯一</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>岗位名称，全局唯一</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>岗位状态（启用 / 停用）</summary>
    public PositionStatus Status { get; set; } = PositionStatus.Enabled;

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
