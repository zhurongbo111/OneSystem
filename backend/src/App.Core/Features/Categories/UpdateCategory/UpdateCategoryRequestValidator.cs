using App.Core.Entities;

using FluentValidation;

namespace App.Core.Features.Categories.UpdateCategory;

/// <summary>
/// 编辑商品分类请求格式校验：只做数据格式检查；名称是否重复等查库约束在 Handler 内
/// </summary>
public sealed class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    /// <summary>
    /// 初始化校验规则
    /// </summary>
    public UpdateCategoryRequestValidator()
    {
        // 入参去首尾空白在 Handler 内统一处理（与 user-management 一致），此处只做格式校验
        // 长度统一取自 CategoryFieldConstraints（与 EF 配置一致，禁止硬编码）
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("分类名称不能为空")
            .Length(CategoryFieldConstraints.NameMinLength, CategoryFieldConstraints.NameMaxLength)
            .WithMessage($"分类名称长度必须在 {CategoryFieldConstraints.NameMinLength} 到 {CategoryFieldConstraints.NameMaxLength} 之间");
    }
}
