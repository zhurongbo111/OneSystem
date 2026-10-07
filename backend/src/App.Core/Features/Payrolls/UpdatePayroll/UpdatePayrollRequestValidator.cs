using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Payrolls.UpdatePayroll;

/// <summary>
/// 编辑工资单请求格式校验：只做数据格式检查；
/// 存在性与状态锁（已发放 `40171`）在 Handler 内
/// </summary>
public sealed class UpdatePayrollRequestValidator : AbstractValidator<UpdatePayrollRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdatePayrollRequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("工资单不能为空");

        // 金额区间统一取自 PayrollFieldConstraints（与新增同源，禁止分叉）
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
