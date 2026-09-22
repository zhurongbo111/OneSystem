using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.TaxRates.UpdateTaxRate;

/// <summary>
/// 编辑税率请求格式校验：只做数据格式检查；
/// 编码 / 名称唯一性等查库约束在 Handler 内
/// </summary>
public sealed class UpdateTaxRateRequestValidator : AbstractValidator<UpdateTaxRateRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdateTaxRateRequestValidator()
    {
        // 路由参数兜底：Controller 以路由值覆盖，避免请求体缺 id 时进入 Handler
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("税率 id 不能为空");

        // 长度 / 区间 / 精度统一取自 TaxRateFieldConstraints（与 EF 配置一致，禁止硬编码）
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("税率编码不能为空")
            .MaximumLength(TaxRateFieldConstraints.CodeMaxLength)
            .WithMessage($"税率编码长度不能超过 {TaxRateFieldConstraints.CodeMaxLength}");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("税率名称不能为空")
            .MaximumLength(TaxRateFieldConstraints.NameMaxLength)
            .WithMessage($"税率名称长度不能超过 {TaxRateFieldConstraints.NameMaxLength}");

        RuleFor(x => x.Rate)
            .InclusiveBetween(TaxRateFieldConstraints.RateMin, TaxRateFieldConstraints.RateMax)
            .WithMessage($"税率必须在 {TaxRateFieldConstraints.RateMin} 到 {TaxRateFieldConstraints.RateMax} 之间")
            .Must(rate => decimal.Round(rate, TaxRateFieldConstraints.RateDecimalPlaces) == rate)
            .WithMessage($"税率小数位不能超过 {TaxRateFieldConstraints.RateDecimalPlaces} 位");

        RuleFor(x => x.Status)
            .Must(status => status is (int)TaxRateStatus.Enabled or (int)TaxRateStatus.Disabled)
            .WithMessage("税率状态值无效");

        RuleFor(x => x.Remark)
            .MaximumLength(TaxRateFieldConstraints.RemarkMaxLength)
            .WithMessage($"备注长度不能超过 {TaxRateFieldConstraints.RemarkMaxLength}");
    }
}