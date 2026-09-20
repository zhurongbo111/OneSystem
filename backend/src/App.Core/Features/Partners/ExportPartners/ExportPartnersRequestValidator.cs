using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Partners.ExportPartners;

/// <summary>
/// 往来单位列表导出请求格式校验：与列表查询规则完全一致（只做数据格式检查）
/// </summary>
public sealed class ExportPartnersRequestValidator : AbstractValidator<ExportPartnersRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public ExportPartnersRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("页码必须从 1 开始");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("每页条数必须在 1 到 100 之间");

        // 长度统一取自 PartnerFieldConstraints（禁止硬编码）
        RuleFor(x => x.Keyword)
            .MaximumLength(PartnerFieldConstraints.KeywordMaxLength)
            .WithMessage($"关键词长度不能超过 {PartnerFieldConstraints.KeywordMaxLength} 个字符")
            .When(x => x.Keyword is not null);

        RuleFor(x => x.Type)
            .Must(t => t is null || t is PartnerType.Supplier or PartnerType.Customer or PartnerType.Both)
            .WithMessage("单位类型值无效");

        RuleFor(x => x.Status)
            .Must(s => s is null || s is PartnerStatus.Enabled or PartnerStatus.Disabled)
            .WithMessage("单位状态值无效");
    }
}
