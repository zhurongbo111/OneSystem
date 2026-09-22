using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Accounts.UpdateAccountStatus;

/// <summary>
/// 会计科目停用 / 启用请求格式校验：只做数据格式检查；科目是否存在在 Handler 内
/// </summary>
public sealed class UpdateAccountStatusRequestValidator : AbstractValidator<UpdateAccountStatusRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdateAccountStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .Must(status => status is (int)AccountStatus.Enabled or (int)AccountStatus.Disabled)
            .WithMessage("科目状态值无效");
    }
}