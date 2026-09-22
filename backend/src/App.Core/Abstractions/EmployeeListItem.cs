using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 员工列表项读模型（仓储联查部门 / 岗位 / 关联账号带出名称，不暴露实体；
/// 列表与导出共用，specs/030-erp-org-employee/design.md §3.1）
/// </summary>
public sealed record EmployeeListItem
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

    /// <summary>所属部门 id，可空</summary>
    public required Guid? DepartmentId { get; init; }

    /// <summary>部门名称（联查带出），未设置部门时为空</summary>
    public required string? DepartmentName { get; init; }

    /// <summary>所属岗位 id，可空</summary>
    public required Guid? PositionId { get; init; }

    /// <summary>岗位名称（联查带出），未设置岗位时为空</summary>
    public required string? PositionName { get; init; }

    /// <summary>入职日期</summary>
    public required DateOnly HireDate { get; init; }

    /// <summary>离职日期，可空</summary>
    public required DateOnly? ResignDate { get; init; }

    /// <summary>在职状态</summary>
    public required EmployeeStatus Status { get; init; }

    /// <summary>关联账号 id，可空</summary>
    public required Guid? UserId { get; init; }

    /// <summary>关联账号显示名（联查带出），未绑定时为空</summary>
    public required string? UserDisplayName { get; init; }

    /// <summary>创建时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>创建人 id（erp-export 导出「创建人」列用，经 IUserRepository 批量换显示名）</summary>
    public required Guid? CreatedBy { get; init; }
}
