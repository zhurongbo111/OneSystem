using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.PurchaseOrders.CreatePurchaseOrder;

/// <summary>
/// 新增采购订单请求格式校验：只做数据格式检查。
/// 存在性 / 类型匹配 / 状态流转等查库约束在 Handler 中判断（后端规则 §4.1）。
/// </summary>
public sealed class CreatePurchaseOrderRequestValidator : AbstractValidator<CreatePurchaseOrderRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public CreatePurchaseOrderRequestValidator()
    {
        RuleFor(x => x.PartnerId).NotEmpty().WithMessage("供应商不能为空");
        RuleFor(x => x.OrderDate).NotNull().WithMessage("下单日期不能为空");

        // 预计到货日期可空，但不得早于下单日期（跨字段校验用匿名类型组合）
        RuleFor(x => new { x.OrderDate, x.ExpectedDate })
            .Must(v => v.ExpectedDate is null || v.ExpectedDate >= v.OrderDate)
            .WithMessage("预计到货日期不能早于下单日期");

        // 明细行：必填非空、1–ItemsMaxCount 行（单一来源 OrderFieldConstraints）
        RuleFor(x => x.Items).NotNull().WithMessage("明细不能为空");
        RuleFor(x => x.Items).Must(items => items is not null && items.Count > 0).WithMessage("明细不能为空");
        RuleFor(x => x.Items).Must(items => items is null || items.Count <= OrderFieldConstraints.ItemsMaxCount)
            .WithMessage($"明细行数不能超过 {OrderFieldConstraints.ItemsMaxCount} 行");

        // 每行：数量 / 单价边界引用 ProductFieldConstraints（同一规则同源，禁止复制常量）。
        // 直接取 x.Items（required 非空集合），勿套 ?? 复合表达式——会使 FluentValidation InferPropertyName 抛异常
        RuleForEach(x => x.Items)
            .ChildRules(item =>
            {
                item.RuleFor(i => i.ProductId).NotEmpty().WithMessage("商品不能为空");
                item.RuleFor(i => i.Quantity)
                    .InclusiveBetween(ProductFieldConstraints.QuantityMinValue, ProductFieldConstraints.QuantityMaxValue)
                    .WithMessage("数量超出允许范围");
                item.RuleFor(i => i.UnitPrice)
                    .InclusiveBetween(ProductFieldConstraints.PriceMinValue, ProductFieldConstraints.PriceMaxValue)
                    .WithMessage("单价超出允许范围");
            });

        RuleFor(x => x.Remark).MaximumLength(OrderFieldConstraints.RemarkMaxLength).When(x => x.Remark is not null)
            .WithMessage("备注超长");
    }
}
