using App.Core.Entities;

namespace App.Core.Features.Categories;

/// <summary>
/// 商品分类实体 → 出参模型 的映射（集中一处，避免各用例重复拼装）
/// </summary>
internal static class CategoryDtoMapper
{
    /// <summary>映射出参（列表 / 新增 / 编辑共用同一模型）</summary>
    public static CategoryDto ToCategoryDto(Category category)
        => new()
        {
            Id = category.Id.ToString(),
            Name = category.Name,
            CreatedAt = category.CreatedAt,
        };
}
