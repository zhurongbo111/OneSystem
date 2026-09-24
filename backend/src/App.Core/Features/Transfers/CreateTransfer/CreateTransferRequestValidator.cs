using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Transfers.CreateTransfer;

/// <summary>
/// 新增调拨单请求格式校验：只做数据格式检查。
/// 存在性 / 仓相同 / 库存等查库约束在 Handler 中判断（后端规则 §4.1）。
/// 相等判定（fromWarehouseId == toWarehouseId → 40126）放 Handler，统一错误码来源，避免两处。
/// 长度与数量规则与采购 / 销售**同源**（OrderFieldConstraints / ProductFieldConstraints）。
/// </summary>
public sealed class CreateTransferRequestValidator : AbstractValidator<CreateTransferRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public CreateTransferRequestValidator()
    {
        // 两仓必填非空（相等判定放 Handler 抛 40126，避免 Validator 与 Handler 两处分叉）
        RuleFor(x => x.FromWarehouseId).NotEmpty().WithMessage("转出仓不能为空");
        RuleFor(x => x.ToWarehouseId).NotEmpty().WithMessage("转入仓不能为空");
        RuleFor(x => x.TransferDate).NotNull().WithMessage("业务日期不能为空");

        // 明细行：必填非空、1–ItemsMaxCount 行（单一来源 OrderFieldConstraints）
        RuleFor(x => x.Items).NotNull().WithMessage("明细不能为空");
        RuleFor(x => x.Items).Must(items => items is not null && items.Count > 0).WithMessage("明细不能为空");
        RuleFor(x => x.Items).Must(items => items is null || items.Count <= OrderFieldConstraints.ItemsMaxCount)
            .WithMessage($"明细行数不能超过 {OrderFieldConstraints.ItemsMaxCount} 行");

        // 每行：数量边界引用 ProductFieldConstraints（同一规则同源，禁止复制常量）。
        // 直接取 x.Items（required 非空集合），勿套 ?? 复合表达式——会使 FluentValidation InferPropertyName 抛异常
        RuleForEach(x => x.Items)
            .ChildRules(item =>
            {
                item.RuleFor(i => i.ProductId).NotEmpty().WithMessage("商品不能为空");
                item.RuleFor(i => i.Quantity)
                    .InclusiveBetween(ProductFieldConstraints.QuantityMinValue, ProductFieldConstraints.QuantityMaxValue)
                    .WithMessage("数量超出允许范围");
            });

        // productId 不重复（同一单据内不允许重复「商品 + 批次」组合，040 前批次恒空 → 即商品不重复）
        RuleFor(x => x.Items)
            .Must(items => items is null || items.Select(i => i.ProductId).Distinct().Count() == items.Count)
            .When(x => x.Items is not null && x.Items.Count > 0)
            .WithMessage("明细中商品不能重复");

        RuleFor(x => x.Remark).MaximumLength(OrderFieldConstraints.RemarkMaxLength).When(x => x.Remark is not null)
            .WithMessage("备注超长");
    }
}
