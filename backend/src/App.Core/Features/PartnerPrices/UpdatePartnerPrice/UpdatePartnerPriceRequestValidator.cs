using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.PartnerPrices.UpdatePartnerPrice;

/// <summary>
/// 编辑客户协议价请求格式校验：格式层规则与新增完全一致（AGENTS.md §4.5 全量覆盖）
/// </summary>
public sealed class UpdatePartnerPriceRequestValidator : AbstractValidator<UpdatePartnerPriceRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdatePartnerPriceRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("协议价 id 不能为空");

        RuleFor(x => x.Price)
            .InclusiveBetween(ProductFieldConstraints.PriceMinValue, ProductFieldConstraints.PriceMaxValue)
            .WithMessage($"协议单价必须在 {ProductFieldConstraints.PriceMinValue} 到 {ProductFieldConstraints.PriceMaxValue} 之间");

        RuleFor(x => x.Remark)
            .MaximumLength(OrderFieldConstraints.RemarkMaxLength)
            .When(x => x.Remark is not null)
            .WithMessage($"备注不能超过 {OrderFieldConstraints.RemarkMaxLength} 个字符");
    }
}