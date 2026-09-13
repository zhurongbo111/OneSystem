using App.Core.Abstractions;

namespace App.Core.Features.Categories.GetCategories;

/// <summary>
/// 分类列表请求（无参用例以空 Request 占位，不定义 Validator）
/// </summary>
public sealed class GetCategoriesRequest : IRequest<IReadOnlyList<CategoryDto>>
{
}
