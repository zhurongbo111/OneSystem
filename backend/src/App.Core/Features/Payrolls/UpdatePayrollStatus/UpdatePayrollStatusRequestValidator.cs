using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Payrolls.UpdatePayrollStatus;

/// <summary>
/// 工资单发放 / 反发放请求格式校验：只做数据格式检查，存在性在 Handler 内
/// </summary>
public sealed class UpdatePayrollStatusRequestValidator : AbstractValidator<UpdatePayrollStatusRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdatePayrollStatusRequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("工资单不能为空");

        RuleFor(x => x.Status)
            .Must(status => status is (int)PayrollStatus.Draft or (int)PayrollStatus.Paid)
            .WithMessage("工资单状态值无效");
    }
}
