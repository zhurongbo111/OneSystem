using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.TaxRates.UpdateTaxRateStatus;

/// <summary>
/// 税率停用 / 启用请求格式校验：只做数据格式检查；税率是否存在在 Handler 内
/// </summary>
public sealed class UpdateTaxRateStatusRequestValidator : AbstractValidator<UpdateTaxRateStatusRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdateTaxRateStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .Must(status => status is (int)TaxRateStatus.Enabled or (int)TaxRateStatus.Disabled)
            .WithMessage("税率状态值无效");
    }
}