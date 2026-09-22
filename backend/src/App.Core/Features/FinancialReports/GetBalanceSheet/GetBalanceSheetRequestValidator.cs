using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.FinancialReports.GetBalanceSheet;

/// <summary>
/// 资产负债表查询请求格式校验：只做数据格式检查
/// </summary>
public sealed class GetBalanceSheetRequestValidator : AbstractValidator<GetBalanceSheetRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public GetBalanceSheetRequestValidator()
    {
        // 期间区间统一取自 PeriodFieldConstraints（禁止硬编码）
        RuleFor(x => x.Year)
            .InclusiveBetween(PeriodFieldConstraints.YearMinValue, PeriodFieldConstraints.YearMaxValue)
            .WithMessage($"年份必须在 {PeriodFieldConstraints.YearMinValue} 到 {PeriodFieldConstraints.YearMaxValue} 之间");

        RuleFor(x => x.Month)
            .InclusiveBetween(PeriodFieldConstraints.MonthMinValue, PeriodFieldConstraints.MonthMaxValue)
            .WithMessage($"月份必须在 {PeriodFieldConstraints.MonthMinValue} 到 {PeriodFieldConstraints.MonthMaxValue} 之间");
    }
}
