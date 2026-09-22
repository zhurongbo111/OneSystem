namespace App.Core.Entities;

/// <summary>
/// 员工实体（对应 PostgreSQL 表 Employees）。
/// **员工 ≠ 账号**：员工经可空、唯一的 <see cref="UserId"/> 绑定一个系统账号（登录一律走 009 的 Users）；
/// 员工不做删除，仅「在职 ↔ 离职」切换，保留历史与审计引用。
/// </summary>
public sealed class Employee
{
    /// <summary>员工 ID</summary>
    public Guid Id { get; set; }

    /// <summary>工号，全局唯一，创建后不可修改</summary>
    public string EmployeeNo { get; set; } = string.Empty;

    /// <summary>姓名</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>性别，可空</summary>
    public Gender? Gender { get; set; }

    /// <summary>手机号，可空；非空时全局唯一</summary>
    public string? Phone { get; set; }

    /// <summary>邮箱，可空；非空时全局唯一</summary>
    public string? Email { get; set; }

    /// <summary>所属部门 id，可空</summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>所属岗位 id，可空</summary>
    public Guid? PositionId { get; set; }

    /// <summary>入职日期（纯日期，无时区语义）</summary>
    public DateOnly HireDate { get; set; }

    /// <summary>离职日期，可空；置离职时为空则由 Handler 补当天</summary>
    public DateOnly? ResignDate { get; set; }

    /// <summary>在职状态（在职 / 离职）</summary>
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;

    /// <summary>关联系统账号 id，可空；非空时全局唯一（一个账号最多被一个员工绑定）</summary>
    public Guid? UserId { get; set; }

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
