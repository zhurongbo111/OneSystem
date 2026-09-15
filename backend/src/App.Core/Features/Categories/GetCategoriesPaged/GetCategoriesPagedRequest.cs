using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.Categories.GetCategoriesPaged;

/// <summary>
/// 分类分页查询请求（搜索 + 分页，分类管理页用）
/// </summary>
public sealed class GetCategoriesPagedRequest : IRequest<PagedResult<CategoryDto>>
{
    /// <summary>页码，从 1 起</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; init; } = 20;

    /// <summary>关键词（名称模糊匹配），可空</summary>
    public string? Keyword { get; init; }
}
