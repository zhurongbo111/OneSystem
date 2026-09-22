using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.AccountingPeriods.GetPeriods;

/// <summary>
/// 会计期间列表查询请求格式校验：只做数据格式检查
/// </summary>
public sealed class GetPeriodsRequestValidator : AbstractValidator<GetPeriodsRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public GetPeriodsRequestValidator()
    {
        // 年份区间统一取自 PeriodFieldConstraints（禁止硬编码）
        RuleFor(x => x.Year)
            .InclusiveBetween(PeriodFieldConstraints.YearMinValue, PeriodFieldConstraints.YearMaxValue)
            .When(x => x.Year is not null)
            .WithMessage($"年份必须在 {PeriodFieldConstraints.YearMinValue} 到 {PeriodFieldConstraints.YearMaxValue} 之间");
    }
}
