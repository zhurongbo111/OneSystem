using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Settlements.CreateSettlement;

/// <summary>
/// 新增收付款单请求格式校验：只做数据格式检查。
/// 存在性 / 往来与方向匹配 / 未结金额等查库约束在 Handler 中判断（后端规则 §4.1）。
/// </summary>
public sealed class CreateSettlementRequestValidator : AbstractValidator<CreateSettlementRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public CreateSettlementRequestValidator()
    {
        RuleFor(x => x.Type)
            .Must(t => t is SettlementType.Receipt or SettlementType.Payment)
            .WithMessage("收付款类型取值非法");

        RuleFor(x => x.PartnerId).NotEmpty().WithMessage("往来单位不能为空");
        RuleFor(x => x.SettlementDate).NotNull().WithMessage("收付日期不能为空");

        RuleFor(x => x.Method)
            .Must(m => m is SettlementMethod.Cash or SettlementMethod.BankTransfer or SettlementMethod.Other)
            .WithMessage("收付款方式取值非法");

        // 核销明细：必填非空、1–ItemsMaxCount 行、同一单据不重复（单一来源 OrderFieldConstraints）
        RuleFor(x => x.Items).NotNull().WithMessage("核销明细不能为空");
        RuleFor(x => x.Items).Must(items => items is not null && items.Count > 0).WithMessage("核销明细不能为空");
        RuleFor(x => x.Items).Must(items => items is null || items.Count <= OrderFieldConstraints.ItemsMaxCount)
            .WithMessage($"核销明细行数不能超过 {OrderFieldConstraints.ItemsMaxCount} 行");
        RuleFor(x => x.Items).Must(items => items is null
                || items.Select(i => (i.OrderType, i.OrderId)).Distinct().Count() == items.Count)
            .WithMessage("同一单据不允许重复核销");

        // 每行：类型 / 单据 id / 金额边界（金额上界与单据金额同源 ProductFieldConstraints）
        RuleForEach(x => x.Items)
            .ChildRules(item =>
            {
                item.RuleFor(i => i.OrderType)
                    .Must(t => t is SettlementOrderType.PurchaseInbound or SettlementOrderType.SalesOutbound
                        or SettlementOrderType.PurchaseReturn or SettlementOrderType.SalesReturn)
                    .WithMessage("被核销单据类型取值非法");
                item.RuleFor(i => i.OrderId).NotEmpty().WithMessage("被核销单据不能为空");
                item.RuleFor(i => i.Amount)
                    .GreaterThan(0m).WithMessage("核销金额必须大于 0")
                    .LessThanOrEqualTo(ProductFieldConstraints.PriceMaxValue).WithMessage("核销金额超出允许范围");
            });

        RuleFor(x => x.Remark).MaximumLength(OrderFieldConstraints.RemarkMaxLength).When(x => x.Remark is not null)
            .WithMessage("备注超长");
    }
}
