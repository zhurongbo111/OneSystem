using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.StockTakes.ExportStockTakes;

/// <summary>
/// 盘点单导出请求格式校验：与列表查询规则完全一致（只做数据格式检查，长度引用 StockTakeFieldConstraints）
/// </summary>
public sealed class ExportStockTakesRequestValidator : AbstractValidator<ExportStockTakesRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public ExportStockTakesRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("页码必须从 1 开始");
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage("每页条数必须在 1 到 100 之间");

        // 长度统一取自 StockTakeFieldConstraints（对齐实际匹配列 TakeNo）
        RuleFor(x => x.Keyword).MaximumLength(StockTakeFieldConstraints.KeywordMaxLength).When(x => x.Keyword is not null)
            .WithMessage("单号关键词超长");

        // 类型可空，非空时必须是合法枚举值
        RuleFor(x => x.Type).Must(t => t is null or StockTakeType.Initial or StockTakeType.Take)
            .WithMessage("单据类型取值非法");

        // 日期范围闭区间：两者都传时 start <= end
        RuleFor(x => new { x.Start, x.End })
            .Must(v => v.Start is null || v.End is null || v.End >= v.Start)
            .WithMessage("结束日期不能早于开始日期");
    }
}
