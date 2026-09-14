using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.Products.UpdateProduct;

/// <summary>
/// 编辑商品用例：校验商品存在 + 分类存在 → 落库（编码不可修改，请求体不含 code）。
/// 停用商品也允许编辑（修改信息后仍可停用）。
/// </summary>
public sealed class UpdateProductRequestHandler : IRequestHandler<UpdateProductRequest, ProductDto>
{
    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// 初始化编辑商品用例处理器
    /// </summary>
    public UpdateProductRequestHandler(
        IProductRepository productRepository,
        ICategoryRepository categoryRepository,
        IInventoryRepository inventoryRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
        _inventoryRepository = inventoryRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    /// <summary>
    /// 处理编辑商品请求
    /// </summary>
    /// <param name="request">编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ProductDto> HandleAsync(UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(request.Id, cancellationToken);
        if (product is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "商品不存在");
        }

        // 查库约束：分类必须存在
        var category = await _categoryRepository.GetByIdAsync(request.CategoryId, cancellationToken);
        if (category is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "商品分类不存在");
        }

        var name = request.Name.Trim();
        var unit = request.Unit.Trim();

        // 编码不可修改，保持原值不变
        product.Name = name;
        product.CategoryId = request.CategoryId;
        product.Unit = unit;
        product.PurchasePrice = request.PurchasePrice;
        product.SalePrice = request.SalePrice;
        product.SafetyStock = request.SafetyStock;
        product.Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();
        product.UpdatedAt = DateTimeOffset.UtcNow;
        product.UpdatedBy = ProductInputNormalizer.CurrentUserId(_currentUser);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _productRepository.UpdateAsync(product, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        // 落库后重读当前库存，返回完整出参
        var stockQuantity = await _inventoryRepository.GetQuantityAsync(product.Id, cancellationToken);
        return ProductDtoMapper.ToProductDto(product, category.Name, stockQuantity);
    }
}
