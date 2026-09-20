using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Products.ExportProducts;

/// <summary>
/// 商品列表导出请求格式校验：与列表查询规则完全一致（只做数据格式检查）
/// </summary>
public sealed class ExportProductsRequestValidator : AbstractValidator<ExportProductsRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public ExportProductsRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("页码必须从 1 开始");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("每页条数必须在 1 到 100 之间");

        // 长度统一取自 ProductFieldConstraints（禁止硬编码）
        RuleFor(x => x.Keyword).MaximumLength(ProductFieldConstraints.KeywordMaxLength).When(x => x.Keyword is not null);

        RuleFor(x => x.Status)
            .Must(s => s is null || s is ProductStatus.Enabled or ProductStatus.Disabled)
            .WithMessage("商品状态值无效");
    }
}
