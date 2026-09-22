using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Employees.UpdateEmployee;

/// <summary>
/// 编辑员工请求格式校验：只做数据格式检查；
/// 唯一性（手机 / 邮箱 / 账号）与部门 / 岗位 / 账号存在性等查库约束在 Handler 内
/// </summary>
public sealed class UpdateEmployeeRequestValidator : AbstractValidator<UpdateEmployeeRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdateEmployeeRequestValidator()
    {
        // 路由参数兜底：Controller 以路由值覆盖，避免请求体缺 id 时进入 Handler
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("员工 id 不能为空");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("姓名不能为空")
            .MaximumLength(EmployeeFieldConstraints.NameMaxLength)
            .WithMessage($"姓名长度不能超过 {EmployeeFieldConstraints.NameMaxLength}");

        RuleFor(x => x.Gender)
            .Must(gender => gender is null or (int)Gender.Unknown or (int)Gender.Male or (int)Gender.Female)
            .WithMessage("性别值无效");

        RuleFor(x => x.Phone)
            .MaximumLength(EmployeeFieldConstraints.PhoneMaxLength)
            .WithMessage($"手机号长度不能超过 {EmployeeFieldConstraints.PhoneMaxLength}")
            .Matches(EmployeeFieldConstraints.PhonePattern).WithMessage("手机号格式不正确")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));

        RuleFor(x => x.Email)
            .MaximumLength(EmployeeFieldConstraints.EmailMaxLength)
            .WithMessage($"邮箱长度不能超过 {EmployeeFieldConstraints.EmailMaxLength}")
            .EmailAddress().WithMessage("邮箱格式不正确")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.HireDate)
            .Must(hireDate => hireDate != default).WithMessage("入职日期不能为空");

        RuleFor(x => x.ResignDate)
            .Must((request, resignDate) => resignDate is null || resignDate.Value >= request.HireDate)
            .WithMessage("离职日期不能早于入职日期");

        RuleFor(x => x.Status)
            .Must(status => status is (int)EmployeeStatus.Active or (int)EmployeeStatus.Resigned)
            .WithMessage("在职状态值无效");

        RuleFor(x => x.Remark)
            .MaximumLength(EmployeeFieldConstraints.RemarkMaxLength)
            .WithMessage($"备注长度不能超过 {EmployeeFieldConstraints.RemarkMaxLength}");
    }
}
