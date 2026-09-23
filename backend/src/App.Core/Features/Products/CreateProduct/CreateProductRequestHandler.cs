using App.Core.Abstractions;
using App.Core.Audit;
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
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化新增商品用例处理器
    /// </summary>
    public CreateProductRequestHandler(
        IProductRepository productRepository,
        ICategoryRepository categoryRepository,
        IWarehouseRepository warehouseRepository,
        IInventoryRepository inventoryRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _productRepository = productRepository;
        _categoryRepository = categoryRepository;
        _warehouseRepository = warehouseRepository;
        _inventoryRepository = inventoryRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
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

        var changeBuilder = new AuditChangeBuilder()
            .Add("code", "商品编码", null, product.Code)
            .Add("name", "商品名称", null, product.Name)
            .Add("categoryId", "所属分类", null, category.Name)
            .Add("unit", "单位", null, product.Unit)
            .Add("purchasePrice", "采购价", null, AuditSummary.Money(product.PurchasePrice))
            .Add("salePrice", "销售价", null, AuditSummary.Money(product.SalePrice))
            .Add("safetyStock", "安全库存", null, AuditSummary.Quantity(product.SafetyStock))
            .Add("remark", "备注", null, product.Remark);

        // 商品与库存初始化行在同一事务内落库
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _productRepository.AddAsync(product, cancellationToken);

            // 为每个启用仓建 0 库存行（038）：安全库存取商品档案阈值作为各仓初始值；
            // 无启用仓不可能（默认仓必存在且不可停用）
            var warehouses = await _warehouseRepository.GetEnabledAsync(cancellationToken);
            foreach (var warehouse in warehouses)
            {
                await _inventoryRepository.EnsureRowAsync(
                    product.Id, warehouse.Id, product.SafetyStock, cancellationToken);
            }

            // 业务写成功后、提交前追加操作日志：与业务同事务，异常回滚则不产生日志
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Product,
                Action = AuditAction.Create,
                ResourceId = product.Id,
                ResourceNo = product.Code,
                Summary = $"创建商品 {product.Code} {product.Name}",
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

        return ProductDtoMapper.ToProductDto(product, category.Name, 0);
    }
}
