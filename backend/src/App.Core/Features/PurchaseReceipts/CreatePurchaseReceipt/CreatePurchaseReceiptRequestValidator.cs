using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.PurchaseReceipts.CreatePurchaseReceipt;

/// <summary>
/// 新增采购入库单请求格式校验：只做数据格式检查。
/// 存在性 / 类型匹配 / 订单状态 / 未收数量等查库约束在 Handler 中判断（后端规则 §4.1）。
/// </summary>
public sealed class CreatePurchaseReceiptRequestValidator : AbstractValidator<CreatePurchaseReceiptRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public CreatePurchaseReceiptRequestValidator()
    {
        RuleFor(x => x.PartnerId).NotEmpty().WithMessage("供应商不能为空");
        RuleFor(x => x.OrderDate).NotNull().WithMessage("业务日期不能为空");

        // 关联订单一致性（格式层）：关联订单时每行必须给出订单明细行 id；不关联时不得给出
        // 业务层（订单存在 / 状态 / 明细归属 / 未收数量）在 Handler 判断
        RuleFor(x => x)
            .Must(request =>
            {
                if (request.Items is null || request.Items.Count == 0)
                {
                    return true; // 明细为空由下方规则负责报错
                }

                return request.OrderId is null
                    ? request.Items.All(i => i.OrderItemId is null)
                    : request.Items.All(i => i.OrderItemId is not null);
            })
            .WithMessage("关联订单与明细行不一致：关联订单时每行必须指定订单明细行，未关联订单时不得指定");

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
