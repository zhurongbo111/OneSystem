using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Employees.UpdateEmployeeStatus;

/// <summary>
/// 员工在职 / 离职切换请求格式校验：只做数据格式检查；员工是否存在在 Handler 内
/// </summary>
public sealed class UpdateEmployeeStatusRequestValidator : AbstractValidator<UpdateEmployeeStatusRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdateEmployeeStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .Must(status => status is (int)EmployeeStatus.Active or (int)EmployeeStatus.Resigned)
            .WithMessage("在职状态值无效");
    }
}
