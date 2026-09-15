using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Products.CreateProduct;

/// <summary>
/// 新增商品用例：校验编码唯一 + 分类存在 → 商品落库并同步初始化库存行（Quantity = 0）。
/// 两步写在同一事务内，避免"有商品无库存行"的中间态。
/// </summary>
public sealed class CreateProductRequestHandler : IRequestHandler<CreateProductRequest, ProductDto>
{
    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// 初始化新增商品用例处理器
    /// </summary>
    public CreateProductRequestHandler(
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
    /// 处理新增商品请求
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ProductDto> HandleAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        var code = request.Code.Trim();
        var name = request.Name.Trim();
        var unit = request.Unit.Trim();

        // 查库约束：编码唯一（大小写不敏感）
        if (await _productRepository.ExistsByCodeAsync(code, null, cancellationToken))
        {
            throw new BusinessException(ErrorCode.ProductCodeExists, "商品编码已存在");
        }

        // 查库约束：分类必须存在
        var category = await _categoryRepository.GetByIdAsync(request.CategoryId, cancellationToken);
        if (category is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "商品分类不存在");
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            CategoryId = request.CategoryId,
            Unit = unit,
            PurchasePrice = request.PurchasePrice,
            SalePrice = request.SalePrice,
            SafetyStock = request.SafetyStock,
            Status = ProductStatus.Enabled,
            Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        // 商品与库存初始化行在同一事务内落库
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _productRepository.AddAsync(product, cancellationToken);
            // 全限定名：Features 下新增 Inventory 用例命名空间后，Inventory 在该处被解析为命名空间而非实体类型
            await _inventoryRepository.AddAsync(new App.Core.Entities.Inventory
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                Quantity = 0,
                UpdatedAt = now,
            }, cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        return ProductDtoMapper.ToProductDto(product, category.Name, 0);
    }
}
