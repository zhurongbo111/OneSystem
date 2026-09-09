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
        RuleFor(x => x.Username)
            .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("用户名不能为空")
            .MaximumLength(50).WithMessage("用户名长度不能超过 50");

        RuleFor(x => x.Password)
            .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("密码不能为空")
            .MaximumLength(128).WithMessage("密码长度不能超过 128");
    }
}
