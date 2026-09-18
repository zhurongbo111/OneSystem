using App.Core.Features.Reports;

using FluentValidation;

namespace App.Core.Features.Costs.RecalculateCosts;

/// <summary>
/// 成本重算请求格式校验：期间先后与上限（上限常量引用 <see cref="ReportFieldConstraints"/>，与报表同源）。
/// </summary>
public sealed class RecalculateCostsRequestValidator : AbstractValidator<RecalculateCostsRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public RecalculateCostsRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => x.Start is null || x.End is null || x.Start <= x.End)
            .WithMessage("开始时间不能晚于结束时间");

        // 期间上限与报表同源（ReportFieldConstraints.MaxRangeDays），防止误传超大区间
        RuleFor(x => x)
            .Must(x => x.Start is null || x.End is null
                || (x.End.Value - x.Start.Value).TotalDays <= ReportFieldConstraints.MaxRangeDays)
            .WithMessage($"重算期间不能超过 {ReportFieldConstraints.MaxRangeDays} 天");
    }
}
