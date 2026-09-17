using FluentValidation;

namespace App.Core.Features.Reports.GetPurchaseSummary;

/// <summary>
/// 采购汇总查询请求格式校验：只做数据格式检查，不访问仓储（design.md §3.5）。
/// </summary>
public sealed class GetPurchaseSummaryRequestValidator : AbstractValidator<GetPurchaseSummaryRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public GetPurchaseSummaryRequestValidator()
    {
        RuleFor(x => x.Start)
            .NotNull().WithMessage("请选择开始日期");

        RuleFor(x => x.End)
            .NotNull().WithMessage("请选择结束日期");

        RuleFor(x => x)
            .Must(x => x.Start is null || x.End is null || x.Start < x.End)
            .WithMessage("开始日期必须早于结束日期");

        RuleFor(x => x)
            .Must(x => x.Start is null || x.End is null
                || (x.End.Value - x.Start.Value).TotalDays <= ReportFieldConstraints.MaxRangeDays)
            .WithMessage($"查询期间不能超过 {ReportFieldConstraints.MaxRangeDays} 天");

        RuleFor(x => x.GroupBy)
            .IsInEnum().WithMessage("分组维度不合法");

        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("页码必须大于等于 1");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("每页条数必须在 1 到 100 之间");
    }
}
