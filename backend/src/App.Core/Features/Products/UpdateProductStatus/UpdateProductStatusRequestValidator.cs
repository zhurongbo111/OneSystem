using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Products.UpdateProductStatus;

/// <summary>
/// 商品停用 / 启用请求格式校验：只做数据格式检查；商品是否存在在 Handler 内
/// </summary>
public sealed class UpdateProductStatusRequestValidator : AbstractValidator<UpdateProductStatusRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdateProductStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .Must(s => s is (int)ProductStatus.Enabled or (int)ProductStatus.Disabled)
            .WithMessage("商品状态值无效");
    }
}
