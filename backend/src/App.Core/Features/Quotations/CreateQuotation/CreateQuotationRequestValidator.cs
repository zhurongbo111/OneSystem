using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Quotations.CreateQuotation;

/// <summary>
/// 新增报价单请求格式校验：只做数据格式检查。
/// 存在性 / 类型匹配 / 状态流转等查库约束在 Handler 中判断（后端规则 §4.1）。
/// </summary>
public sealed class CreateQuotationRequestValidator : AbstractValidator<CreateQuotationRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public CreateQuotationRequestValidator()
    {
        RuleFor(x => x.PartnerId).NotEmpty().WithMessage("客户不能为空");
        RuleFor(x => x.QuotationDate).NotNull().WithMessage("报价日期不能为空");

        // 有效期可空，但不得早于报价日期（报价日期按 UTC 日历日解释，与前端 toUtcMidnight 一致）
        RuleFor(x => new { x.QuotationDate, x.ValidUntil })
            .Must(v => v.ValidUntil is null || v.ValidUntil >= DateOnly.FromDateTime(v.QuotationDate.UtcDateTime))
            .WithMessage("有效期不能早于报价日期");

        // 明细行：必填非空、1–ItemsMaxCount 行（单一来源 OrderFieldConstraints）
        RuleFor(x => x.Items).NotNull().WithMessage("明细不能为空");
        RuleFor(x => x.Items).Must(items => items is not null && items.Count > 0).WithMessage("明细不能为空");
        RuleFor(x => x.Items).Must(items => items is null || items.Count <= OrderFieldConstraints.ItemsMaxCount)
            .WithMessage($"明细行数不能超过 {OrderFieldConstraints.ItemsMaxCount} 行");

        RuleForEach(x => x.Items)
            .ChildRules(item =>
            {
                item.RuleFor(i => i.ProductId).NotEmpty().WithMessage("商品不能为空");
                item.RuleFor(i => i.Quantity)
                    .InclusiveBetween(ProductFieldConstraints.QuantityMinValue, ProductFieldConstraints.QuantityMaxValue)
                    .WithMessage("数量超出允许范围");
                item.RuleFor(i => i.UnitPrice)
                    .InclusiveBetween(ProductFieldConstraints.PriceMinValue, ProductFieldConstraints.PriceMaxValue)
                    .WithMessage("单价超出允许范围");
            });

        RuleFor(x => x.Remark).MaximumLength(OrderFieldConstraints.RemarkMaxLength).When(x => x.Remark is not null)
            .WithMessage("备注超长");
    }
}
