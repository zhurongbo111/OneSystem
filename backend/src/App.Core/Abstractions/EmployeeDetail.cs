using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 员工详情读模型（员工全字段 + 联查带出的部门 / 岗位 / 关联账号名称）；
/// 详情、编辑回显与写用例出参共用（specs/030-erp-org-employee/design.md §3.1）
/// </summary>
public sealed record EmployeeDetail
{
    /// <summary>员工 ID</summary>
    public required Guid Id { get; init; }

    /// <summary>工号</summary>
    public required string EmployeeNo { get; init; }

    /// <summary>姓名</summary>
    public required string Name { get; init; }

    /// <summary>性别，可空</summary>
    public required Gender? Gender { get; init; }

    /// <summary>手机号，可空</summary>
    public required string? Phone { get; init; }

    /// <summary>邮箱，可空</summary>
    public required string? Email { get; init; }

    /// <summary>所属部门 id，可空</summary>
    public required Guid? DepartmentId { get; init; }

    /// <summary>部门名称（联查带出）</summary>
    public required string? DepartmentName { get; init; }

    /// <summary>所属岗位 id，可空</summary>
    public required Guid? PositionId { get; init; }

    /// <summary>岗位名称（联查带出）</summary>
    public required string? PositionName { get; init; }

    /// <summary>入职日期</summary>
    public required DateOnly HireDate { get; init; }

    /// <summary>离职日期，可空</summary>
    public required DateOnly? ResignDate { get; init; }

    /// <summary>在职状态</summary>
    public required EmployeeStatus Status { get; init; }

    /// <summary>关联账号 id，可空</summary>
    public required Guid? UserId { get; init; }

    /// <summary>关联账号显示名（联查带出）</summary>
    public required string? UserDisplayName { get; init; }

    /// <summary>备注，可空</summary>
    public required string? Remark { get; init; }

    /// <summary>创建时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>更新时间</summary>
    public required DateTimeOffset UpdatedAt { get; init; }
}
