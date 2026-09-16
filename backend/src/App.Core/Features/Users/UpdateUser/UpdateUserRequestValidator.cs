using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Users.UpdateUser;

/// <summary>
/// 编辑用户请求格式校验：只做数据格式检查；存在性与唯一性等查库约束在 Handler 内
/// </summary>
public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("用户 id 不能为空");

        // 长度 / 正则统一取自 UserFieldConstraints（与 EF 配置及创建用例一致）
        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("显示名不能为空")
            .MaximumLength(UserFieldConstraints.DisplayNameMaxLength)
            .WithMessage($"显示名长度不能超过 {UserFieldConstraints.DisplayNameMaxLength}");

        RuleFor(x => x.Email)
            .MaximumLength(UserFieldConstraints.EmailMaxLength)
            .WithMessage($"邮箱长度不能超过 {UserFieldConstraints.EmailMaxLength}")
            .EmailAddress().WithMessage("邮箱格式不正确")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.Phone)
            .MaximumLength(UserFieldConstraints.PhoneMaxLength)
            .WithMessage($"手机号长度不能超过 {UserFieldConstraints.PhoneMaxLength}")
            .Matches(UserFieldConstraints.PhonePattern).WithMessage("手机号格式不正确")
            .When(x => !string.IsNullOrWhiteSpace(x.Phone));
    }
}
