using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.StockTakes.CreateStockTake;

/// <summary>
/// 新增盘点 / 期初建账单请求格式校验：只做数据格式检查（长度 / 边界引用 StockTakeFieldConstraints）。
/// 存在性 / 停用 / 期初限制 / 差异计算等查库约束在 Handler 中判断（后端规则 §4.1）。
/// </summary>
public sealed class CreateStockTakeRequestValidator : AbstractValidator<CreateStockTakeRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public CreateStockTakeRequestValidator()
    {
        // 类型可空即非法（必填），非空时必须是合法枚举值
        RuleFor(x => x.Type).Must(t => t is StockTakeType.Initial or StockTakeType.Take)
            .WithMessage("单据类型取值非法");

        RuleFor(x => x.TakeDate).NotNull().WithMessage("盘点日期不能为空");

        // 明细行：必填非空、1–ItemsMaxCount 行、productId 不重复（单一来源 StockTakeFieldConstraints）
        RuleFor(x => x.Items).NotNull().WithMessage("明细不能为空");
        RuleFor(x => x.Items).Must(items => items is not null && items.Count > 0).WithMessage("明细不能为空");
        RuleFor(x => x.Items).Must(items => items is null || items.Count <= StockTakeFieldConstraints.ItemsMaxCount)
            .WithMessage($"明细行数不能超过 {StockTakeFieldConstraints.ItemsMaxCount} 行");
        RuleFor(x => x.Items).Must(items => items is null || items.Select(i => i.ProductId).Distinct().Count() == items.Count)
            .WithMessage("同一单据内商品不可重复");

        // 每行：商品必填、实盘数量 0–上界（实盘可为 0，引用 StockTakeFieldConstraints）。
        // 直接取 x.Items（required 非空集合），勿套 ?? 复合表达式——会使 FluentValidation InferPropertyName 抛异常
        RuleForEach(x => x.Items)
            .ChildRules(item =>
            {
                item.RuleFor(i => i.ProductId).NotEmpty().WithMessage("商品不能为空");
                item.RuleFor(i => i.ActualQuantity)
                    .InclusiveBetween(StockTakeFieldConstraints.ActualQuantityMinValue, StockTakeFieldConstraints.ActualQuantityMaxValue)
                    .WithMessage("实盘数量超出允许范围");
            });

        RuleFor(x => x.Remark).MaximumLength(StockTakeFieldConstraints.RemarkMaxLength).When(x => x.Remark is not null)
            .WithMessage("备注超长");
    }
}
