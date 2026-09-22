using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Employees.UpdateEmployee;

/// <summary>
/// 编辑员工请求（不含工号：工号创建后不可修改，AGENTS.md §4.5；
/// 其余字段全量覆盖语义，空串一律清空）
/// </summary>
public sealed class UpdateEmployeeRequest : IRequest<EmployeeDetailDto>
{
    /// <summary>员工 id（取自路由，请求体缺省时由 Controller 覆盖）</summary>
    public Guid Id { get; init; }

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

    /// <summary>在职状态（0 离职 / 1 在职）</summary>
    public int Status { get; init; } = (int)EmployeeStatus.Active;

    /// <summary>关联系统账号 id，可空；非空时全局唯一（一个账号最多绑一个员工）</summary>
    public Guid? UserId { get; init; }

    /// <summary>备注，可空</summary>
    public string? Remark { get; init; }
}
