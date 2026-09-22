using App.Core.Entities;
using App.Core.Finance;

using FluentValidation;

namespace App.Core.Features.AccountMappings.UpdateAccountMappings;

/// <summary>
/// 科目映射维护请求格式校验：只做数据格式检查；
/// 科目存在性 / 末级 / 启用（40157）在 Handler
/// </summary>
public sealed class UpdateAccountMappingsRequestValidator : AbstractValidator<UpdateAccountMappingsRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdateAccountMappingsRequestValidator()
    {
        RuleFor(x => x.Items)
            .NotNull().WithMessage("科目映射不能为空")
            .Must(items => items is { Count: > 0 })
            .WithMessage("科目映射不能为空")
            // 全量覆盖语义：须与已登记的映射键集合完全一致（不重不漏）
            .Must(items => items is not null
                && items.Select(i => i.Key).Distinct(StringComparer.Ordinal).Count() == items.Count
                && items.Select(i => i.Key).ToHashSet(StringComparer.Ordinal)
                    .SetEquals(AccountMappingKeys.All.Select(d => d.Key)))
            .WithMessage("科目映射须提交全部映射键且不可重复");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            // 长度取自 AccountMappingFieldConstraints（禁止硬编码）
            item.RuleFor(i => i.Key)
                .NotEmpty().WithMessage("映射键不能为空")
                .MaximumLength(AccountMappingFieldConstraints.KeyMaxLength)
                .WithMessage($"映射键长度不能超过 {AccountMappingFieldConstraints.KeyMaxLength}");

            item.RuleFor(i => i.AccountId)
                .NotEmpty().WithMessage("目标科目不能为空");
        });
    }
}
