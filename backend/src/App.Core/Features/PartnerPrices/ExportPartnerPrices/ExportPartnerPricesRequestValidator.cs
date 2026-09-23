using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.PartnerPrices.ExportPartnerPrices;

/// <summary>
/// 客户价格列表导出请求格式校验：与列表查询规则完全一致（只做数据格式检查）
/// </summary>
public sealed class ExportPartnerPricesRequestValidator : AbstractValidator<ExportPartnerPricesRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public ExportPartnerPricesRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("页码必须从 1 开始");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("每页条数必须在 1 到 100 之间");

        // 长度统一取自 PartnerFieldConstraints（与列表 query 同源，禁止硬编码）
        RuleFor(x => x.Keyword)
            .MaximumLength(PartnerFieldConstraints.KeywordMaxLength)
            .WithMessage($"关键词长度不能超过 {PartnerFieldConstraints.KeywordMaxLength} 个字符")
            .When(x => x.Keyword is not null);

        RuleFor(x => x.PartnerId)
            .NotEqual(Guid.Empty)
            .WithMessage("客户 ID 无效")
            .When(x => x.PartnerId is not null);

        RuleFor(x => x.ProductId)
            .NotEqual(Guid.Empty)
            .WithMessage("商品 ID 无效")
            .When(x => x.ProductId is not null);
    }
}
