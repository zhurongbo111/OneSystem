namespace App.Core.Entities;

/// <summary>
/// 月度工资单实体（对应 PostgreSQL 表 Payrolls，<c>specs/044-erp-hcm-payroll/design.md</c> §2.2）。
/// 一个员工一个月一条（唯一索引 <c>(EmployeeId, Year, Month)</c>）；<see cref="EmployeeName"/> 为姓名快照。
/// 口径从简：不做个税 / 社保，<see cref="Deduction"/> 由人工填写；
/// <see cref="NetPay"/> 一律由 Handler 按 <c>基本工资 + 津贴 − 扣款</c> 重算（不信任前端）。
/// </summary>
public sealed class Payroll
{
    /// <summary>工资单 ID</summary>
    public Guid Id { get; set; }

    /// <summary>员工 id（FK → Employees(Id)）</summary>
    public Guid EmployeeId { get; set; }

    /// <summary>员工姓名快照</summary>
    public string EmployeeName { get; set; } = string.Empty;

    /// <summary>年（2000–2100）</summary>
    public int Year { get; set; }

    /// <summary>月（1–12）</summary>
    public int Month { get; set; }

    /// <summary>基本工资</summary>
    public decimal BaseSalary { get; set; }

    /// <summary>津贴（默认 0）</summary>
    public decimal Allowance { get; set; }

    /// <summary>扣款（默认 0）</summary>
    public decimal Deduction { get; set; }

    /// <summary>实发 = 基本工资 + 津贴 − 扣款（后端计算）</summary>
    public decimal NetPay { get; set; }

    /// <summary>状态（草稿 / 已发放）</summary>
    public PayrollStatus Status { get; set; } = PayrollStatus.Draft;

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
