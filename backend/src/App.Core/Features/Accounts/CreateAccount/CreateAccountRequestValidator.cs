using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Accounts.CreateAccount;

/// <summary>
/// 新增会计科目请求格式校验：只做数据格式检查；
/// 编码是否重复、上级是否存在等查库约束在 Handler 内
/// </summary>
public sealed class CreateAccountRequestValidator : AbstractValidator<CreateAccountRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public CreateAccountRequestValidator()
    {
        // 长度 / 区间统一取自 AccountFieldConstraints（与 EF 配置一致，禁止硬编码）
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("科目编码不能为空")
            .MaximumLength(AccountFieldConstraints.CodeMaxLength)
            .WithMessage($"科目编码长度不能超过 {AccountFieldConstraints.CodeMaxLength}");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("科目名称不能为空")
            .MaximumLength(AccountFieldConstraints.NameMaxLength)
            .WithMessage($"科目名称长度不能超过 {AccountFieldConstraints.NameMaxLength}");

        RuleFor(x => x.Category)
            .Must(category => Enum.IsDefined(typeof(AccountCategory), category))
            .WithMessage("科目类别值无效");

        RuleFor(x => x.Direction)
            .Must(direction => Enum.IsDefined(typeof(AccountDirection), direction))
            .WithMessage("余额方向值无效");

        RuleFor(x => x.SortOrder)
            .InclusiveBetween(AccountFieldConstraints.SortOrderMinValue, AccountFieldConstraints.SortOrderMaxValue)
            .WithMessage($"排序必须在 {AccountFieldConstraints.SortOrderMinValue} 到 {AccountFieldConstraints.SortOrderMaxValue} 之间");

        RuleFor(x => x.Status)
            .Must(status => status is (int)AccountStatus.Enabled or (int)AccountStatus.Disabled)
            .WithMessage("科目状态值无效");

        RuleFor(x => x.Remark)
            .MaximumLength(AccountFieldConstraints.RemarkMaxLength)
            .WithMessage($"备注长度不能超过 {AccountFieldConstraints.RemarkMaxLength}");
    }
}