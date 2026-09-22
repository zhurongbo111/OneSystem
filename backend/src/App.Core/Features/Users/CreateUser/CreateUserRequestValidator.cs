using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Users.CreateUser;

/// <summary>
/// 新增用户请求格式校验：只做数据格式检查；用户名 / 邮箱 / 手机号是否重复与角色是否存在等查库约束在 Handler 内
/// </summary>
public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public CreateUserRequestValidator()
    {
        // 长度 / 正则 / 密码区间统一取自 UserFieldConstraints（与 EF 配置一致，禁止硬编码）
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("用户名不能为空")
            .Matches(UserFieldConstraints.UsernamePattern)
            .WithMessage($"用户名只能由 {UserFieldConstraints.UsernameMinLength}-{UserFieldConstraints.UsernameMaxLength} 位字母、数字或下划线组成");

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

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("密码不能为空")
            .Length(UserFieldConstraints.PasswordMinLength, UserFieldConstraints.PasswordMaxLength)
            .WithMessage($"密码长度必须在 {UserFieldConstraints.PasswordMinLength} 到 {UserFieldConstraints.PasswordMaxLength} 之间");

        // 角色：至少一个（避免"无角色黑洞"），上限取自 RoleFieldConstraints
        RuleFor(x => x.RoleIds)
            .NotEmpty().WithMessage("请至少选择一个角色")
            .Must(ids => ids.Count <= RoleFieldConstraints.RolesPerUserMaxCount)
            .WithMessage($"角色数量不能超过 {RoleFieldConstraints.RolesPerUserMaxCount} 个");
    }
}
