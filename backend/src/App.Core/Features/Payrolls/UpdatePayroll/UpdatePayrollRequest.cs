using App.Core.Abstractions;

namespace App.Core.Features.Payrolls.UpdatePayroll;

/// <summary>
/// 编辑工资单请求（全量覆盖，AGENTS.md §4.5）。
/// **不可改字段**：员工与期间（`employeeId` / `year` / `month` 不在请求体内，取原值），
/// 故请求不承接唯一性冲突（`40170` 只在新增与批量生成时产生）。
/// </summary>
public sealed class UpdatePayrollRequest : IRequest<PayrollDetailDto>
{
    /// <summary>工资单 id（由路由提供，请求体可缺省）</summary>
    public Guid Id { get; init; }

    /// <summary>基本工资（0–9999999.99）</summary>
    public decimal BaseSalary { get; init; }

    /// <summary>津贴（0–9999999.99）</summary>
    public decimal Allowance { get; init; }

    /// <summary>扣款（0–9999999.99）</summary>
    public decimal Deduction { get; init; }

    /// <summary>备注，可空（缺省 / 空白视为清空）</summary>
    public string? Remark { get; init; }
}
