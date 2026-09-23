using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Quotations.UpdateQuotation;

/// <summary>
/// 编辑报价单请求格式校验：与新增同源（同一字段同一规则，禁止分叉；后端规则 §5.3）
/// </summary>
public sealed class UpdateQuotationRequestValidator : AbstractValidator<UpdateQuotationRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdateQuotationRequestValidator()
    {
        RuleFor(x => x.PartnerId).NotEmpty().WithMessage("客户不能为空");
        RuleFor(x => x.QuotationDate).NotNull().WithMessage("报价日期不能为空");

        RuleFor(x => new { x.QuotationDate, x.ValidUntil })
            .Must(v => v.ValidUntil is null || v.ValidUntil >= DateOnly.FromDateTime(v.QuotationDate.UtcDateTime))
            .WithMessage("有效期不能早于报价日期");

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
