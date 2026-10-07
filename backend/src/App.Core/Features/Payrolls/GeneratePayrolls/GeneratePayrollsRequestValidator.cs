using FluentValidation;

namespace App.Core.Features.Payrolls.GeneratePayrolls;

/// <summary>
/// 批量生成工资单请求格式校验：只做数据格式检查，不访问仓储
/// </summary>
public sealed class GeneratePayrollsRequestValidator : AbstractValidator<GeneratePayrollsRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public GeneratePayrollsRequestValidator()
    {
        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100).WithMessage("年份必须在 2000 到 2100 之间");

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12).WithMessage("月份必须在 1 到 12 之间");
    }
}
