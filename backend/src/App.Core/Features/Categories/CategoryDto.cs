namespace App.Core.Features.Categories;

/// <summary>
/// 商品分类出参模型（列表 / 新增 / 编辑共用）
/// </summary>
public sealed class CategoryDto
{
    /// <summary>分类 ID</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>分类名称</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; init; }
}
