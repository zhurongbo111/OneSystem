using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Reports.ExportStockBalance;

/// <summary>
/// 库存余额表导出请求格式校验：与报表查询规则完全一致（只做数据格式检查）
/// </summary>
public sealed class ExportStockBalanceRequestValidator : AbstractValidator<ExportStockBalanceRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public ExportStockBalanceRequestValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("页码必须大于等于 1");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("每页条数必须在 1 到 100 之间");

        // keyword 匹配商品编码（32）/ 名称（50），长度上限取两者较大者（同源常量，禁止硬编码）
        RuleFor(x => x.Keyword)
            .MaximumLength(ProductFieldConstraints.KeywordMaxLength)
            .WithMessage($"关键词长度不能超过 {ProductFieldConstraints.KeywordMaxLength}");
    }
}
