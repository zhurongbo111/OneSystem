using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Payrolls.CreatePayroll;

/// <summary>
/// 新增工资单请求格式校验：只做数据格式检查；
/// 员工存在性与期间唯一性（`40170`）等查库约束在 Handler 内
/// </summary>
public sealed class CreatePayrollRequestValidator : AbstractValidator<CreatePayrollRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public CreatePayrollRequestValidator()
    {
        RuleFor(x => x.EmployeeId)
            .NotEmpty().WithMessage("员工不能为空");

        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100).WithMessage("年份必须在 2000 到 2100 之间");

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12).WithMessage("月份必须在 1 到 12 之间");

        // 金额区间统一取自 PayrollFieldConstraints（与 EF 配置一致，禁止硬编码）
        RuleFor(x => x.BaseSalary)
            .InclusiveBetween(0m, PayrollFieldConstraints.AmountMaxValue)
            .WithMessage($"基本工资必须在 0 到 {PayrollFieldConstraints.AmountMaxValue} 之间");

        RuleFor(x => x.Allowance)
            .InclusiveBetween(0m, PayrollFieldConstraints.AmountMaxValue)
            .WithMessage($"津贴必须在 0 到 {PayrollFieldConstraints.AmountMaxValue} 之间");

        RuleFor(x => x.Deduction)
            .InclusiveBetween(0m, PayrollFieldConstraints.AmountMaxValue)
            .WithMessage($"扣款必须在 0 到 {PayrollFieldConstraints.AmountMaxValue} 之间");

        RuleFor(x => x.Remark)
            .MaximumLength(PayrollFieldConstraints.RemarkMaxLength)
            .WithMessage($"备注长度不能超过 {PayrollFieldConstraints.RemarkMaxLength}");
    }
}
