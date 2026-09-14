using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.Products.GetProducts;

/// <summary>
/// 商品分页查询用例：关键词（编码 / 名称模糊）+ 分类 + 状态筛选，创建时间倒序；
/// 库存列由仓储联查带出，低库存标记由 Handler 计算（SafetyStock &gt; 0 且 Stock &lt; SafetyStock）
/// </summary>
public sealed class GetProductsRequestHandler : IRequestHandler<GetProductsRequest, PagedResult<ProductDto>>
{
    private readonly IProductRepository _productRepository;

    /// <summary>
    /// 初始化商品分页查询用例处理器
    /// </summary>
    public GetProductsRequestHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    /// <summary>
    /// 处理商品分页查询请求
    /// </summary>
    /// <param name="request">分页查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<ProductDto>> HandleAsync(GetProductsRequest request, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _productRepository.GetPagedAsync(
            request.Keyword,
            request.CategoryId,
            request.Status,
            request.Page,
            request.PageSize,
            cancellationToken);

        return new PagedResult<ProductDto>
        {
            Items = items.Select(ProductDtoMapper.ToProductDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
