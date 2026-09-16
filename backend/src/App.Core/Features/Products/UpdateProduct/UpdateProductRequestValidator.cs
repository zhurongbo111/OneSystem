using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Products.UpdateProduct;

/// <summary>
/// 编辑商品请求格式校验：只做数据格式检查；商品 / 分类是否存在等查库约束在 Handler 内。
/// 长度 / 取值边界统一取自 ProductFieldConstraints（与 EF 配置一致，禁止硬编码）。
/// 编码不可修改，故无 code 相关规则。
/// </summary>
public sealed class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdateProductRequestValidator()
    {
        // 入参去首尾空白在 Handler 内统一处理（与 user-management 一致），此处只做格式校验
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("商品名称不能为空")
            .Length(ProductFieldConstraints.NameMinLength, ProductFieldConstraints.NameMaxLength)
            .WithMessage($"商品名称长度必须在 {ProductFieldConstraints.NameMinLength} 到 {ProductFieldConstraints.NameMaxLength} 之间");

        RuleFor(x => x.CategoryId)
            .NotEmpty().WithMessage("商品分类不能为空");

        RuleFor(x => x.Unit)
            .NotEmpty().WithMessage("计量单位不能为空")
            .MaximumLength(ProductFieldConstraints.UnitMaxLength)
            .WithMessage($"计量单位长度不能超过 {ProductFieldConstraints.UnitMaxLength} 个字符");

        RuleFor(x => x.PurchasePrice)
            .InclusiveBetween(ProductFieldConstraints.PriceMinValue, ProductFieldConstraints.PriceMaxValue)
            .WithMessage($"默认采购价必须在 {ProductFieldConstraints.PriceMinValue} 到 {ProductFieldConstraints.PriceMaxValue} 之间");

        RuleFor(x => x.SalePrice)
            .InclusiveBetween(ProductFieldConstraints.PriceMinValue, ProductFieldConstraints.PriceMaxValue)
            .WithMessage($"默认销售价必须在 {ProductFieldConstraints.PriceMinValue} 到 {ProductFieldConstraints.PriceMaxValue} 之间");

        RuleFor(x => x.SafetyStock)
            .InclusiveBetween(ProductFieldConstraints.SafetyStockMinValue, ProductFieldConstraints.SafetyStockMaxValue)
            .WithMessage($"安全库存阈值必须在 {ProductFieldConstraints.SafetyStockMinValue} 到 {ProductFieldConstraints.SafetyStockMaxValue} 之间");

        RuleFor(x => x.Remark)
            .MaximumLength(ProductFieldConstraints.RemarkMaxLength)
            .WithMessage($"备注长度不能超过 {ProductFieldConstraints.RemarkMaxLength} 个字符")
            .When(x => x.Remark is not null);
    }
}
