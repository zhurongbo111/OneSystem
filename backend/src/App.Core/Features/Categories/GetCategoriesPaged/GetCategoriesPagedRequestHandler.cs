using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.Categories.GetCategoriesPaged;

/// <summary>
/// 分类分页查询用例：名称模糊匹配筛选，创建时间正序
/// </summary>
public sealed class GetCategoriesPagedRequestHandler : IRequestHandler<GetCategoriesPagedRequest, PagedResult<CategoryDto>>
{
    private readonly ICategoryRepository _categoryRepository;

    /// <summary>
    /// 初始化分类分页查询用例处理器
    /// </summary>
    public GetCategoriesPagedRequestHandler(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    /// <summary>
    /// 处理分类分页查询请求
    /// </summary>
    /// <param name="request">分页查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<CategoryDto>> HandleAsync(GetCategoriesPagedRequest request, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _categoryRepository.GetPagedAsync(
            request.Keyword,
            request.Page,
            request.PageSize,
            cancellationToken);

        return new PagedResult<CategoryDto>
        {
            Items = items.Select(CategoryDtoMapper.ToCategoryDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
