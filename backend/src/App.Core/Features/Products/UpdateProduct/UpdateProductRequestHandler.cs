using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
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
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化编辑商品用例处理器
    /// </summary>
    public UpdateProductRequestHandler(
        IProductRepository productRepository,
        ICategoryRepository categoryRepository,
        IInventoryRepository inventoryRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
        _inventoryRepository = inventoryRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
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

        // 变更前后快照：分类可能同步调整，旧分类名需单独读一次（取 request.CategoryId 之前读到的原值）
        var oldCategory = await _categoryRepository.GetByIdAsync(product.CategoryId, cancellationToken);
        var beforeName = product.Name;
        var beforeCategoryId = product.CategoryId;
        var beforeUnit = product.Unit;
        var beforePurchasePrice = product.PurchasePrice;
        var beforeSalePrice = product.SalePrice;
        var beforeSafetyStock = product.SafetyStock;
        var beforeRemark = product.Remark;
        var now = DateTimeOffset.UtcNow;

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
        product.UpdatedAt = now;
        product.UpdatedBy = _currentUser.UserId();

        var changeBuilder = beforeCategoryId == product.CategoryId
            ? new AuditChangeBuilder()
            : new AuditChangeBuilder().Add("categoryId", "所属分类", oldCategory?.Name, category.Name);
        changeBuilder
            .Add("name", "商品名称", beforeName, product.Name)
            .Add("unit", "单位", beforeUnit, product.Unit)
            .Add("purchasePrice", "采购价", AuditSummary.Money(beforePurchasePrice), AuditSummary.Money(product.PurchasePrice))
            .Add("salePrice", "销售价", AuditSummary.Money(beforeSalePrice), AuditSummary.Money(product.SalePrice))
            .Add("safetyStock", "安全库存", AuditSummary.Quantity(beforeSafetyStock), AuditSummary.Quantity(product.SafetyStock))
            .Add("remark", "备注", beforeRemark, product.Remark);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _productRepository.UpdateAsync(product, cancellationToken);

            // 业务写成功后、提交前追加操作日志：与业务同事务，异常回滚则不产生日志
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Product,
                Action = AuditAction.Update,
                ResourceId = product.Id,
                ResourceNo = product.Code,
                Summary = $"编辑商品 {product.Code} {product.Name}",
                Changes = changeBuilder.Build(),
                ChangesTruncated = changeBuilder.Truncated,
                UtcNow = now,
            }, cancellationToken);

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
