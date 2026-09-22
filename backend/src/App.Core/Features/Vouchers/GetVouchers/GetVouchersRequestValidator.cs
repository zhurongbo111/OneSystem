using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Vouchers.GetVouchers;

/// <summary>
/// 凭证分页查询请求格式校验：只做数据格式检查
/// </summary>
public sealed class GetVouchersRequestValidator : AbstractValidator<GetVouchersRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public GetVouchersRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("页码必须从 1 开始");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("每页条数必须在 1 到 100 之间");

        // 长度统一取自 VoucherFieldConstraints（禁止硬编码；覆盖凭证号 / 摘要的匹配列长）
        RuleFor(x => x.Keyword).MaximumLength(VoucherFieldConstraints.KeywordMaxLength).When(x => x.Keyword is not null);

        RuleFor(x => x.SourceType)
            .Must(t => t is null || Enum.IsDefined(typeof(VoucherSourceType), t.Value))
            .WithMessage("凭证来源类型取值非法");

        // 期间筛选区间统一取自 PeriodFieldConstraints（禁止硬编码）
        RuleFor(x => x.Year)
            .InclusiveBetween(PeriodFieldConstraints.YearMinValue, PeriodFieldConstraints.YearMaxValue)
            .When(x => x.Year is not null)
            .WithMessage($"年份必须在 {PeriodFieldConstraints.YearMinValue} 到 {PeriodFieldConstraints.YearMaxValue} 之间");

        RuleFor(x => x.Month)
            .InclusiveBetween(PeriodFieldConstraints.MonthMinValue, PeriodFieldConstraints.MonthMaxValue)
            .When(x => x.Month is not null)
            .WithMessage($"月份必须在 {PeriodFieldConstraints.MonthMinValue} 到 {PeriodFieldConstraints.MonthMaxValue} 之间");

        // 年与月须同时提供（只传一个无法定位期间）
        RuleFor(x => new { x.Year, x.Month })
            .Must(v => (v.Year is null) == (v.Month is null))
            .WithMessage("期间筛选须同时提供年份与月份");
    }
}
