using App.Core.Entities;
using FluentValidation;

namespace App.Core.Features.Users.ResetPassword;

/// <summary>
/// 重置密码请求格式校验：只做数据格式检查；用户是否存在等查库约束在 Handler 内
/// </summary>
public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("用户 id 不能为空");

        // 密码区间统一取自 UserFieldConstraints（与登录、创建用例一致）
        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("新密码不能为空")
            .Length(UserFieldConstraints.PasswordMinLength, UserFieldConstraints.PasswordMaxLength)
            .WithMessage($"新密码长度必须在 {UserFieldConstraints.PasswordMinLength} 到 {UserFieldConstraints.PasswordMaxLength} 之间");
    }
}
