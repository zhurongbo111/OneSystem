using FluentValidation;

namespace App.Core.Features.PartnerPrices.GetEffectivePrices;

/// <summary>
/// 批量取生效价请求格式校验：只做数据格式检查（商品存在性由 Handler 判定）
/// </summary>
public sealed class GetEffectivePricesRequestValidator : AbstractValidator<GetEffectivePricesRequest>
{
    /// <summary>单次批量取价的商品数量上限（开单明细行上限 × 余量，见 design.md §3.5）</summary>
    public const int ProductIdsMaxCount = 100;

    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public GetEffectivePricesRequestValidator()
    {
        RuleFor(x => x.PartnerId).NotEmpty().WithMessage("客户不能为空");

        RuleFor(x => x.ProductIds)
            .Must(ids => ids is not null && ids.Count > 0)
            .WithMessage("商品不能为空")
            .Must(ids => ids is null || ids.Distinct().Count() <= ProductIdsMaxCount)
            .WithMessage($"商品数量不能超过 {ProductIdsMaxCount} 个");
    }
}