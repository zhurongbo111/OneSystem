using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.SalesReturns.CreateSalesReturn;

/// <summary>
/// 新增销售退货单请求格式校验：只做数据格式检查。
/// 存在性 / 类型匹配等查库约束在 Handler 中判断（后端规则 §4.1）。
/// 长度与数量 / 单价规则与采购退货 / 采购 / 销售**同源**（OrderFieldConstraints / ProductFieldConstraints）。
/// </summary>
public sealed class CreateSalesReturnRequestValidator : AbstractValidator<CreateSalesReturnRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public CreateSalesReturnRequestValidator()
    {
        RuleFor(x => x.PartnerId).NotEmpty().WithMessage("客户不能为空");
        RuleFor(x => x.ReturnDate).NotNull().WithMessage("业务日期不能为空");

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

                // 批次（040）：batchId 与 newBatchNo 互斥（同时提供 → 40000，防歧义）
                item.RuleFor(i => i)
                    .Must(line => line.BatchId is null || string.IsNullOrWhiteSpace(line.NewBatchNo))
                    .WithMessage("批次 id 与就地新建批次号不可同时提供");

                // 就地新建批次：批次号格式（同 BatchFieldConstraints 常量）+ 日期先后
                item.RuleFor(i => i.NewBatchNo)
                    .NotEmpty()
                    .Length(BatchFieldConstraints.BatchNoMinLength, BatchFieldConstraints.BatchNoMaxLength)
                    .Matches(BatchFieldConstraints.BatchNoPattern)
                    .WithMessage("批次号格式不正确（1–50 位字母 / 数字 / 下划线 / 连字符）")
                    .When(i => i.NewBatchNo is not null);
                item.RuleFor(i => i)
                    .Must(line => line.NewExpiryDate is null || line.NewProductionDate is null
                        || line.NewExpiryDate.Value >= line.NewProductionDate.Value)
                    .WithMessage("到期日不能早于生产日期")
                    .When(i => i.NewBatchNo is not null);
            });

        RuleFor(x => x.Remark).MaximumLength(OrderFieldConstraints.RemarkMaxLength).When(x => x.Remark is not null)
            .WithMessage("备注超长");
    }
}
