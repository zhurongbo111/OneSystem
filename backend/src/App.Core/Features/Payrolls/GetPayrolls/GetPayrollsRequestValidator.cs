using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Payrolls.GetPayrolls;

/// <summary>
/// 工资单分页列表请求格式校验：只做数据格式检查，不访问仓储
/// </summary>
public sealed class GetPayrollsRequestValidator : AbstractValidator<GetPayrollsRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public GetPayrollsRequestValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("页码必须大于等于 1");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("每页条数必须在 1 到 100 之间");

        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100).When(x => x.Year is not null)
            .WithMessage("年份必须在 2000 到 2100 之间");

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12).When(x => x.Month is not null)
            .WithMessage("月份必须在 1 到 12 之间");

        RuleFor(x => x.Status)
            .Must(status => status is null or (int)PayrollStatus.Draft or (int)PayrollStatus.Paid)
            .WithMessage("工资单状态值无效");
    }
}
