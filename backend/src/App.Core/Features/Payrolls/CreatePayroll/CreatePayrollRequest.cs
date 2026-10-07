using App.Core.Abstractions;

namespace App.Core.Features.Payrolls.CreatePayroll;

/// <summary>
/// 新增工资单请求（一个员工一个月一条；实发由后端按口径重算）
/// </summary>
public sealed class CreatePayrollRequest : IRequest<PayrollDetailDto>
{
    /// <summary>员工 id（必填；须存在）</summary>
    public Guid EmployeeId { get; init; }

    /// <summary>年（2000–2100）</summary>
    public int Year { get; init; }

    /// <summary>月（1–12）</summary>
    public int Month { get; init; }

    /// <summary>基本工资（0–9999999.99）</summary>
    public decimal BaseSalary { get; init; }

    /// <summary>津贴（0–9999999.99，缺省 0）</summary>
    public decimal Allowance { get; init; }

    /// <summary>扣款（0–9999999.99，缺省 0）</summary>
    public decimal Deduction { get; init; }

    /// <summary>备注，可空</summary>
    public string? Remark { get; init; }
}
