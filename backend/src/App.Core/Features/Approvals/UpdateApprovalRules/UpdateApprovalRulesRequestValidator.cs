using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Approvals.UpdateApprovalRules;

/// <summary>
/// 审批规则保存请求格式校验（design.md §3.6）：1–4 项、单据类型不重复且合法、
/// 阈值区间取 <see cref="ProductFieldConstraints"/>（价格上下界同源，禁止硬编码）
/// </summary>
public sealed class UpdateApprovalRulesRequestValidator : AbstractValidator<UpdateApprovalRulesRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdateApprovalRulesRequestValidator()
    {
        RuleFor(x => x.Rules)
            .NotNull()
            .WithMessage("审批规则不能为空")
            .Must(rules => rules.Count is >= 1 and <= 4)
            .WithMessage("审批规则必须为 1 到 4 项")
            .Must(rules => rules.Select(r => r.OrderType).Distinct().Count() == rules.Count)
            .WithMessage("同一单据类型不能重复配置规则");

        RuleForEach(x => x.Rules).ChildRules(item =>
        {
            item.RuleFor(r => r.OrderType)
                .Must(t => t is SettlementOrderType.PurchaseInbound or SettlementOrderType.SalesOutbound
                    or SettlementOrderType.PurchaseReturn or SettlementOrderType.SalesReturn)
                .WithMessage("单据类型取值非法");

            item.RuleFor(r => r.ThresholdAmount)
                .GreaterThan(ProductFieldConstraints.PriceMinValue)
                .WithMessage("审批阈值必须大于 0")
                .LessThanOrEqualTo(ProductFieldConstraints.PriceMaxValue)
                .WithMessage($"审批阈值不能超过 {ProductFieldConstraints.PriceMaxValue}");
        });
    }
}
