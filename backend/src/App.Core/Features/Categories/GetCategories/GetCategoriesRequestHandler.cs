using App.Core.Abstractions;

namespace App.Core.Features.Categories.GetCategories;

/// <summary>
/// 分类列表用例：全量查询（下拉 / 筛选用，量小），创建时间正序
/// </summary>
public sealed class GetCategoriesRequestHandler : IRequestHandler<GetCategoriesRequest, IReadOnlyList<CategoryDto>>
{
    private readonly ICategoryRepository _categoryRepository;

    /// <summary>
    /// 初始化分类列表用例处理器
    /// </summary>
    public GetCategoriesRequestHandler(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    /// <summary>
    /// 处理分类列表请求
    /// </summary>
    /// <param name="request">列表请求（空参数）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<IReadOnlyList<CategoryDto>> HandleAsync(GetCategoriesRequest request, CancellationToken cancellationToken = default)
    {
        var categories = await _categoryRepository.GetAllAsync(cancellationToken);
        return categories.Select(c => new CategoryDto
        {
            Id = c.Id.ToString(),
            Name = c.Name,
            CreatedAt = c.CreatedAt,
        }).ToList();
    }
}
