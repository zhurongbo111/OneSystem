using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.StockMovements.GetStockMovements;

/// <summary>
/// 库存流水查询请求格式校验：只做数据格式检查，不访问仓储
/// </summary>
public sealed class GetStockMovementsRequestValidator : AbstractValidator<GetStockMovementsRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public GetStockMovementsRequestValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("页码必须大于等于 1");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("每页条数必须在 1 到 100 之间");

        // keyword 匹配 SourceNo 列，长度与列长同源（OrderFieldConstraints，design §2.4）
        RuleFor(x => x.Keyword)
            .MaximumLength(OrderFieldConstraints.KeywordMaxLength)
            .WithMessage($"单号关键词长度不能超过 {OrderFieldConstraints.KeywordMaxLength}");

        // 可空枚举：绑定失败时模型绑定先报错，此处兜底合法枚举值
        RuleFor(x => x.Type)
            .IsInEnum()
            .When(x => x.Type.HasValue)
            .WithMessage("变动类型不合法");

        RuleFor(x => x)
            .Must(x => x.Start is null || x.End is null || x.Start <= x.End)
            .WithMessage("开始时间不能晚于结束时间");
    }
}
