using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Invoices.CreateInvoice;

/// <summary>
/// 登记发票请求格式校验：只做数据格式检查。
/// 发票号唯一性 / 往来类型匹配 / 单据存在性 / 方向与往来一致性 / 未开票金额等查库约束在 Handler 中判断
/// （后端规则 §4.1）。
/// </summary>
public sealed class CreateInvoiceRequestValidator : AbstractValidator<CreateInvoiceRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public CreateInvoiceRequestValidator()
    {
        // 长度 / 区间 / 精度统一取自 InvoiceFieldConstraints 与既有常量（禁止硬编码）
        RuleFor(x => x.InvoiceNo)
            .NotEmpty().WithMessage("发票号不能为空")
            .MaximumLength(InvoiceFieldConstraints.InvoiceNoMaxLength)
            .WithMessage($"发票号长度不能超过 {InvoiceFieldConstraints.InvoiceNoMaxLength}");

        RuleFor(x => x.Type)
            .Must(t => t is InvoiceType.Purchase or InvoiceType.Sales)
            .WithMessage("发票类型取值非法");

        RuleFor(x => x.PartnerId).NotEmpty().WithMessage("往来单位不能为空");
        RuleFor(x => x.InvoiceDate).NotNull().WithMessage("开票日期不能为空");

        RuleFor(x => x.AmountExcludingTax)
            .GreaterThan(ProductFieldConstraints.PriceMinValue).WithMessage("不含税金额必须大于 0")
            .LessThanOrEqualTo(ProductFieldConstraints.PriceMaxValue).WithMessage("不含税金额超出允许范围");

        RuleFor(x => x.TaxRate)
            .InclusiveBetween(InvoiceFieldConstraints.TaxRateMinValue, InvoiceFieldConstraints.TaxRateMaxValue)
            .WithMessage($"税率必须在 {InvoiceFieldConstraints.TaxRateMinValue} 到 {InvoiceFieldConstraints.TaxRateMaxValue} 之间")
            .Must(rate => decimal.Round(rate, InvoiceFieldConstraints.TaxRateDecimalPlaces) == rate)
            .WithMessage($"税率小数位不能超过 {InvoiceFieldConstraints.TaxRateDecimalPlaces} 位");

        // 关联明细：必填非空、1–ItemsMaxCount 行、同一单据不重复（单一来源 OrderFieldConstraints）
        RuleFor(x => x.Items).NotNull().WithMessage("关联单据不能为空");
        RuleFor(x => x.Items).Must(items => items is not null && items.Count > 0).WithMessage("关联单据不能为空");
        RuleFor(x => x.Items).Must(items => items is null || items.Count <= OrderFieldConstraints.ItemsMaxCount)
            .WithMessage($"关联单据行数不能超过 {OrderFieldConstraints.ItemsMaxCount} 行");
        RuleFor(x => x.Items).Must(items => items is null
                || items.Select(i => (i.OrderType, i.OrderId)).Distinct().Count() == items.Count)
            .WithMessage("同一单据不允许重复关联");

        // 每行：类型 / 单据 id / 金额边界（金额上界与单据金额同源 ProductFieldConstraints）
        RuleForEach(x => x.Items)
            .ChildRules(item =>
            {
                item.RuleFor(i => i.OrderType)
                    .Must(t => t is SettlementOrderType.PurchaseInbound or SettlementOrderType.SalesOutbound
                        or SettlementOrderType.PurchaseReturn or SettlementOrderType.SalesReturn)
                    .WithMessage("被关联单据类型取值非法");
                item.RuleFor(i => i.OrderId).NotEmpty().WithMessage("被关联单据不能为空");
                item.RuleFor(i => i.Amount)
                    .GreaterThan(0m).WithMessage("开票金额必须大于 0")
                    .LessThanOrEqualTo(ProductFieldConstraints.PriceMaxValue).WithMessage("开票金额超出允许范围");
            });

        RuleFor(x => x.Remark).MaximumLength(OrderFieldConstraints.RemarkMaxLength).When(x => x.Remark is not null)
            .WithMessage("备注超长");
    }
}