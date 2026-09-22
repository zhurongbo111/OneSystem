using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.TaxRates.GetTaxRates;

/// <summary>
/// 税率分页列表请求格式校验：只做数据格式检查，不访问仓储
/// </summary>
public sealed class GetTaxRatesRequestValidator : AbstractValidator<GetTaxRatesRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public GetTaxRatesRequestValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("页码必须大于等于 1");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("每页条数必须在 1 到 100 之间");

        RuleFor(x => x.Status)
            .Must(status => status is null or (int)TaxRateStatus.Enabled or (int)TaxRateStatus.Disabled)
            .WithMessage("税率状态值无效");

        // 关键词匹配编码 / 名称，长度上限取二者较大者（名称列），超列长不可能命中
        RuleFor(x => x.Keyword)
            .MaximumLength(TaxRateFieldConstraints.NameMaxLength)
            .WithMessage($"关键词长度不能超过 {TaxRateFieldConstraints.NameMaxLength}");
    }
}