using App.Core.Abstractions;

namespace App.Core.Features.Categories.UpdateCategory;

/// <summary>
/// 编辑商品分类请求
/// </summary>
public sealed class UpdateCategoryRequest : IRequest<CategoryDto>
{
    /// <summary>分类 id</summary>
    public Guid Id { get; init; }

    /// <summary>分类名称（唯一，大小写不敏感，排除自身）</summary>
    public string Name { get; init; } = string.Empty;
}
