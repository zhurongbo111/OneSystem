using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Employees.CreateEmployee;

/// <summary>
/// 新增员工请求（工号创建后不可修改；部门 / 岗位 / 关联账号均可空）
/// </summary>
public sealed class CreateEmployeeRequest : IRequest<EmployeeDetailDto>
{
    /// <summary>工号（全局唯一，创建后不可修改）</summary>
    public string EmployeeNo { get; init; } = string.Empty;

    /// <summary>姓名</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>性别（0 未填 / 1 男 / 2 女），可空</summary>
    public int? Gender { get; init; }

    /// <summary>手机号，可空；非空时全局唯一</summary>
    public string? Phone { get; init; }

    /// <summary>邮箱，可空；非空时全局唯一</summary>
    public string? Email { get; init; }

    /// <summary>所属部门 id，可空</summary>
    public Guid? DepartmentId { get; init; }

    /// <summary>所属岗位 id，可空</summary>
    public Guid? PositionId { get; init; }

    /// <summary>入职日期（必填）</summary>
    public DateOnly HireDate { get; init; }

    /// <summary>离职日期，可空（状态为离职且为空时由 Handler 补当天）</summary>
    public DateOnly? ResignDate { get; init; }

    /// <summary>在职状态（0 离职 / 1 在职，默认在职）</summary>
    public int Status { get; init; } = (int)EmployeeStatus.Active;

    /// <summary>关联系统账号 id，可空；非空时全局唯一（一个账号最多绑一个员工）</summary>
    public Guid? UserId { get; init; }

    /// <summary>备注，可空</summary>
    public string? Remark { get; init; }
}
