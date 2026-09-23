using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.PartnerPrices.CreatePartnerPrice;

/// <summary>
/// 新增客户协议价请求格式校验：只做数据格式检查（存在性 / 唯一性由 Handler 判定）
/// </summary>
public sealed class CreatePartnerPriceRequestValidator : AbstractValidator<CreatePartnerPriceRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public CreatePartnerPriceRequestValidator()
    {
        RuleFor(x => x.PartnerId).NotEmpty().WithMessage("客户不能为空");
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("商品不能为空");

        // 协议价与商品价格同源上下界（ProductFieldConstraints），精度 numeric(18,2)
        RuleFor(x => x.Price)
            .InclusiveBetween(ProductFieldConstraints.PriceMinValue, ProductFieldConstraints.PriceMaxValue)
            .WithMessage($"协议单价必须在 {ProductFieldConstraints.PriceMinValue} 到 {ProductFieldConstraints.PriceMaxValue} 之间");

        RuleFor(x => x.Remark)
            .MaximumLength(OrderFieldConstraints.RemarkMaxLength)
            .When(x => x.Remark is not null)
            .WithMessage($"备注不能超过 {OrderFieldConstraints.RemarkMaxLength} 个字符");
    }
}