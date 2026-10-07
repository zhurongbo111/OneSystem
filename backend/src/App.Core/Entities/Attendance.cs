namespace App.Core.Entities;

/// <summary>
/// 考勤登记实体（对应 PostgreSQL 表 Attendances，<c>specs/044-erp-hcm-payroll/design.md</c> §2.1）。
/// 只登记请假 / 加班（无审批、无打卡）；<see cref="EmployeeName"/> 为**姓名快照**，员工改名不影响历史记录。
/// 同一员工 + 同一类型 + 日期区间不得重叠（业务校验在 Handler）。
/// </summary>
public sealed class Attendance
{
    /// <summary>考勤记录 ID</summary>
    public Guid Id { get; set; }

    /// <summary>员工 id（FK → Employees(Id)，不可被删除的员工引用）</summary>
    public Guid EmployeeId { get; set; }

    /// <summary>员工姓名快照</summary>
    public string EmployeeName { get; set; } = string.Empty;

    /// <summary>考勤类型（请假 / 加班）</summary>
    public AttendanceType Type { get; set; }

    /// <summary>起始日（纯日期）</summary>
    public DateOnly StartDate { get; set; }

    /// <summary>结束日（纯日期，不得早于 <see cref="StartDate"/>）</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>事由，可空</summary>
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
