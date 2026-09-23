using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.BankAccounts.UpdateBankAccount;

/// <summary>
/// 编辑资金账户请求格式校验：规则与新增同源（同一套常量），只做数据格式检查；
/// 存在性 / 编码唯一等查库约束在 Handler 内（specs/034-erp-cash/design.md §3.5）
/// </summary>
public sealed class UpdateBankAccountRequestValidator : AbstractValidator<UpdateBankAccountRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdateBankAccountRequestValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("资金账户 id 不能为空");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("账户编码不能为空")
            .MaximumLength(BankAccountFieldConstraints.CodeMaxLength)
            .WithMessage($"账户编码长度不能超过 {BankAccountFieldConstraints.CodeMaxLength}");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("账户名称不能为空")
            .MaximumLength(BankAccountFieldConstraints.NameMaxLength)
            .WithMessage($"账户名称长度不能超过 {BankAccountFieldConstraints.NameMaxLength}");

        RuleFor(x => x.Type)
            .Must(type => type is (int)BankAccountType.Cash or (int)BankAccountType.Bank)
            .WithMessage("资金账户类型值无效");

        RuleFor(x => x.BankName)
            .NotEmpty().WithMessage("开户行不能为空")
            .When(x => x.Type == (int)BankAccountType.Bank)
            .MaximumLength(BankAccountFieldConstraints.BankNameMaxLength)
            .WithMessage($"开户行长度不能超过 {BankAccountFieldConstraints.BankNameMaxLength}");

        RuleFor(x => x.AccountNo)
            .MaximumLength(BankAccountFieldConstraints.AccountNoMaxLength)
            .WithMessage($"银行账号长度不能超过 {BankAccountFieldConstraints.AccountNoMaxLength}");

        RuleFor(x => x.InitialBalance)
            .InclusiveBetween(BankAccountFieldConstraints.InitialBalanceMin, BankAccountFieldConstraints.InitialBalanceMax)
            .WithMessage($"初始余额必须在 {BankAccountFieldConstraints.InitialBalanceMin} 到 {BankAccountFieldConstraints.InitialBalanceMax} 之间")
            .Must(balance => decimal.Round(balance, BankAccountFieldConstraints.AmountDecimalPlaces) == balance)
            .WithMessage($"初始余额小数位不能超过 {BankAccountFieldConstraints.AmountDecimalPlaces} 位");

        RuleFor(x => x.Status)
            .Must(status => status is (int)BankAccountStatus.Enabled or (int)BankAccountStatus.Disabled)
            .WithMessage("资金账户状态值无效");

        RuleFor(x => x.Remark)
            .MaximumLength(BankAccountFieldConstraints.RemarkMaxLength)
            .WithMessage($"备注长度不能超过 {BankAccountFieldConstraints.RemarkMaxLength}");
    }
}
