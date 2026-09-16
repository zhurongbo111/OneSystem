using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Auth.Login;

/// <summary>
/// 登录请求格式校验：只做数据格式检查，不访问仓储 / 查库；
/// 账号是否存在、密码是否正确等依赖数据的约束在 <see cref="LoginRequestHandler"/> 中判断。
/// </summary>
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    /// <summary>
    /// 初始化登录校验规则
    /// </summary>
    public LoginRequestValidator()
    {
        // 长度 / 密码区间统一取自 UserFieldConstraints（与创建、重置及数据库约束一致）
        RuleFor(x => x.Username)
            .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("用户名不能为空")
            .MaximumLength(UserFieldConstraints.UsernameMaxLength)
            .WithMessage($"用户名长度不能超过 {UserFieldConstraints.UsernameMaxLength}");

        RuleFor(x => x.Password)
            .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("密码不能为空")
            .Length(UserFieldConstraints.PasswordMinLength, UserFieldConstraints.PasswordMaxLength)
            .WithMessage($"密码长度必须在 {UserFieldConstraints.PasswordMinLength} 到 {UserFieldConstraints.PasswordMaxLength} 之间");
    }
}
