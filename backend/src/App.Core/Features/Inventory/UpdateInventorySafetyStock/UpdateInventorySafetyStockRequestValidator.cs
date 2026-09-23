using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Inventory.UpdateInventorySafetyStock;

/// <summary>
/// 维护仓级安全库存请求格式校验：商品 / 仓库必填，阈值区间取自 WarehouseFieldConstraints（与商品安全库存同源）
/// </summary>
public sealed class UpdateInventorySafetyStockRequestValidator : AbstractValidator<UpdateInventorySafetyStockRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdateInventorySafetyStockRequestValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty().WithMessage("商品 id 不能为空");
        RuleFor(x => x.WarehouseId).NotEmpty().WithMessage("仓库 id 不能为空");

        RuleFor(x => x.SafetyStock)
            .InclusiveBetween(WarehouseFieldConstraints.SafetyStockMinValue, WarehouseFieldConstraints.SafetyStockMaxValue)
            .WithMessage($"安全库存必须在 {WarehouseFieldConstraints.SafetyStockMinValue} 到 {WarehouseFieldConstraints.SafetyStockMaxValue} 之间");
    }
}
