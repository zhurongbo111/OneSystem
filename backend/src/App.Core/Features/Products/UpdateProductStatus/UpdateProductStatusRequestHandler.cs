using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Products.UpdateProductStatus;

/// <summary>
/// 商品停用 / 启用用例：校验商品存在 → 更新状态。
/// 停用后不可被新单据选择，但保留全部历史数据；启用后可再次被选择。
/// </summary>
public sealed class UpdateProductStatusRequestHandler : IRequestHandler<UpdateProductStatusRequest, ProductDto>
{
    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IInventoryRepository _inventoryRepository;

    /// <summary>
    /// 初始化商品停用 / 启用用例处理器
    /// </summary>
    public UpdateProductStatusRequestHandler(
        IProductRepository productRepository,
        ICategoryRepository categoryRepository,
        IInventoryRepository inventoryRepository)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
        _inventoryRepository = inventoryRepository;
    }

    /// <summary>
    /// 处理商品停用 / 启用请求
    /// </summary>
    /// <param name="request">停用 / 启用请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ProductDto> HandleAsync(UpdateProductStatusRequest request, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken);
        if (product is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "商品不存在");
        }

        var category = await _categoryRepository.GetByIdAsync(product.CategoryId, cancellationToken);
        if (category is null)
        {
            // 数据异常：商品引用了不存在的分类，按 40400 返回便于排查
            throw new BusinessException(ErrorCode.NotFound, "商品引用的分类不存在");
        }

        product.Status = request.Status == (int)ProductStatus.Disabled
            ? ProductStatus.Disabled
            : ProductStatus.Enabled;
        product.UpdatedAt = DateTimeOffset.UtcNow;

        await _productRepository.UpdateAsync(product, cancellationToken);

        var stockQuantity = await _inventoryRepository.GetQuantityAsync(product.Id, cancellationToken);
        return ProductDtoMapper.ToProductDto(product, category.Name, stockQuantity);
    }
}
