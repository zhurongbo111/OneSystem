namespace App.Core.Features.Employees;

/// <summary>
/// 员工列表项出参模型（列表与导出共用）。
/// 枚举统一以**整型**输出（EmployeeStatus：0 离职 / 1 在职；Gender：0 未填 / 1 男 / 2 女）；
/// <see cref="StatusText"/> 为派生文案，供前端直接渲染状态标签。
/// </summary>
public sealed class EmployeeListItemDto
{
    /// <summary>员工 ID</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>工号</summary>
    public string EmployeeNo { get; init; } = string.Empty;

    /// <summary>姓名</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>性别（0 未填 / 1 男 / 2 女），可空</summary>
    public int? Gender { get; init; }

    /// <summary>手机号</summary>
    public string? Phone { get; init; }

    /// <summary>所属部门 id</summary>
    public string? DepartmentId { get; init; }

    /// <summary>部门名称</summary>
    public string? DepartmentName { get; init; }

    /// <summary>所属岗位 id</summary>
    public string? PositionId { get; init; }

    /// <summary>岗位名称</summary>
    public string? PositionName { get; init; }

    /// <summary>入职日期</summary>
    public DateOnly HireDate { get; init; }

    /// <summary>离职日期，可空</summary>
    public DateOnly? ResignDate { get; init; }

    /// <summary>在职状态（0 离职 / 1 在职）</summary>
    public int Status { get; init; }

    /// <summary>在职状态文案（在职 / 离职）</summary>
    public string StatusText { get; init; } = string.Empty;

    /// <summary>关联账号 id</summary>
    public string? UserId { get; init; }

    /// <summary>关联账号显示名</summary>
    public string? UserDisplayName { get; init; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; init; }
}
