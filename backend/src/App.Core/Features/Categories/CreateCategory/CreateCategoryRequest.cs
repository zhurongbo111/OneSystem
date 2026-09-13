using App.Core.Abstractions;

namespace App.Core.Features.Categories.CreateCategory;

/// <summary>
/// 新增商品分类请求
/// </summary>
public sealed class CreateCategoryRequest : IRequest<CategoryDto>
{
    /// <summary>分类名称（唯一，大小写不敏感）</summary>
    public string Name { get; init; } = string.Empty;
}
