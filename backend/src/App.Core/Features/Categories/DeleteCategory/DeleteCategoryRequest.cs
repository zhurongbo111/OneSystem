using App.Core.Abstractions;

namespace App.Core.Features.Categories.DeleteCategory;

/// <summary>
/// 删除商品分类请求（被商品引用的分类不可删除，返回 CategoryInUse 业务错误）
/// </summary>
public sealed class DeleteCategoryRequest : IRequest<object?>
{
    /// <summary>分类 id</summary>
    public Guid Id { get; init; }
}
