using FluentValidation;

namespace App.Core.Features.AccountingPeriods.ReversePeriod;

/// <summary>
/// 会计期间反结账请求格式校验：只做数据格式检查（存在性在 Handler）
/// </summary>
public sealed class ReversePeriodRequestValidator : AbstractValidator<ReversePeriodRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public ReversePeriodRequestValidator()
    {
        RuleFor(x => x.Id).NotEmpty().WithMessage("期间 id 不能为空");
    }
}
