using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.BankAccounts.UpdateBankAccountStatus;

/// <summary>
/// 资金账户启停请求格式校验
/// </summary>
public sealed class UpdateBankAccountStatusRequestValidator : AbstractValidator<UpdateBankAccountStatusRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdateBankAccountStatusRequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("资金账户 id 不能为空");

        RuleFor(x => x.Status)
            .Must(status => status is (int)BankAccountStatus.Enabled or (int)BankAccountStatus.Disabled)
            .WithMessage("资金账户状态值无效");
    }
}
