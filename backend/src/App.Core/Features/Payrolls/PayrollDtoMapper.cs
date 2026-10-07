using App.Core.Entities;

namespace App.Core.Features.Payrolls;

/// <summary>
/// 工资单出参映射（集中一处，避免各用例重复拼装）。
/// 派生字段：<c>statusText</c>（草稿 / 已发放）随映射一并计算；
/// 实发 <c>netPay</c> 为实体字段（由 Handler 按口径重算后落库），映射不重算。
/// </summary>
internal static class PayrollDtoMapper
{
    /// <summary>工资单实体 → 列表项出参</summary>
    public static PayrollListItemDto ToPayrollListItemDto(Payroll payroll)
        => new()
        {
            Id = payroll.Id.ToString(),
            EmployeeId = payroll.EmployeeId.ToString(),
            EmployeeName = payroll.EmployeeName,
            Year = payroll.Year,
            Month = payroll.Month,
            BaseSalary = payroll.BaseSalary,
            Allowance = payroll.Allowance,
            Deduction = payroll.Deduction,
            NetPay = payroll.NetPay,
            Status = (int)payroll.Status,
            StatusText = StatusText(payroll.Status),
            Remark = payroll.Remark,
            CreatedAt = payroll.CreatedAt,
        };

    /// <summary>工资单实体 → 详情出参</summary>
    public static PayrollDetailDto ToPayrollDetailDto(Payroll payroll)
        => new()
        {
            Id = payroll.Id.ToString(),
            EmployeeId = payroll.EmployeeId.ToString(),
            EmployeeName = payroll.EmployeeName,
            Year = payroll.Year,
            Month = payroll.Month,
            BaseSalary = payroll.BaseSalary,
            Allowance = payroll.Allowance,
            Deduction = payroll.Deduction,
            NetPay = payroll.NetPay,
            Status = (int)payroll.Status,
            StatusText = StatusText(payroll.Status),
            Remark = payroll.Remark,
            CreatedAt = payroll.CreatedAt,
            UpdatedAt = payroll.UpdatedAt,
        };

    /// <summary>状态文案</summary>
    private static string StatusText(PayrollStatus status)
        => status == PayrollStatus.Draft ? "草稿" : "已发放";
}
