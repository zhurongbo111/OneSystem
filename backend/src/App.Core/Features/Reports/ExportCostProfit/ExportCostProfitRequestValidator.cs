using FluentValidation;

namespace App.Core.Features.Reports.ExportCostProfit;

/// <summary>
/// 成本与毛利报表导出请求格式校验：与报表查询规则完全一致（期间必填与先后、期间上限、分组维度、分页边界）
/// </summary>
public sealed class ExportCostProfitRequestValidator : AbstractValidator<ExportCostProfitRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public ExportCostProfitRequestValidator()
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
