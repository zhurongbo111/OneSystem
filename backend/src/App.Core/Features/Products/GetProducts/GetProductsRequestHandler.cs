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

        var pageItems = items.Select(ToDto).ToList();
        return new PagedResult<ProductDto>
        {
            Items = pageItems,
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }

    /// <summary>
    /// 列表项读模型转出参模型，计算低库存标记
    /// </summary>
    private static ProductDto ToDto(ProductListItem item)
        => new()
        {
            Id = item.Id.ToString(),
            Code = item.Code,
            Name = item.Name,
            CategoryId = item.CategoryId.ToString(),
            CategoryName = item.CategoryName,
            Unit = item.Unit,
            PurchasePrice = item.PurchasePrice,
            SalePrice = item.SalePrice,
            SafetyStock = item.SafetyStock,
            StockQuantity = item.StockQuantity,
            IsBelowSafetyStock = item.SafetyStock > 0 && item.StockQuantity < item.SafetyStock,
            Status = (int)item.Status,
            Remark = null,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt,
        };
}
