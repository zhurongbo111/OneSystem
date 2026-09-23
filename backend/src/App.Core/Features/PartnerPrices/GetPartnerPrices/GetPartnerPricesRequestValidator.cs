using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.PartnerPrices.GetPartnerPrices;

/// <summary>
/// 客户价格分页查询请求格式校验：只做数据格式检查
/// </summary>
public sealed class GetPartnerPricesRequestValidator : AbstractValidator<GetPartnerPricesRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public GetPartnerPricesRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("页码必须从 1 开始");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("每页条数必须在 1 到 100 之间");

        // 关键词实际匹配列：客户名 / 商品编码 / 商品名称，取三者最大列长（PartnerFieldConstraints.KeywordMaxLength）
        RuleFor(x => x.Keyword)
            .MaximumLength(PartnerFieldConstraints.KeywordMaxLength)
            .When(x => x.Keyword is not null)
            .WithMessage($"关键词不能超过 {PartnerFieldConstraints.KeywordMaxLength} 个字符");
    }
}