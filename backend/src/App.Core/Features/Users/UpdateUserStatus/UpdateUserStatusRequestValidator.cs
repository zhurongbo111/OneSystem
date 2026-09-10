using FluentValidation;

namespace App.Core.Features.Users.UpdateUserStatus;

/// <summary>
/// 启用 / 禁用用户请求格式校验：状态取值必须为 0 或 1
/// </summary>
public sealed class UpdateUserStatusRequestValidator : AbstractValidator<UpdateUserStatusRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdateUserStatusRequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("用户 id 不能为空");

        RuleFor(x => x.Status)
            .Must(status => status is 0 or 1).WithMessage("状态只能是 0（禁用）或 1（启用）");
    }
}
