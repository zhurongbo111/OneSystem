using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Reports.GetStockBalance;

/// <summary>
/// 库存余额表查询请求格式校验：只做数据格式检查，不访问仓储（design.md §3.5）。
/// </summary>
public sealed class GetStockBalanceRequestValidator : AbstractValidator<GetStockBalanceRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public GetStockBalanceRequestValidator()
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
