using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Inventory.UpdateInventorySafetyStock;

/// <summary>
/// 维护仓级安全库存用例（038）：校验商品 / 仓库存在且该仓有库存行 → 写入仓级阈值 → 记操作日志。
/// 仓级阈值是低库存判定的唯一来源（商品档案阈值仅作新建库存行的初始值）。
/// </summary>
public sealed class UpdateInventorySafetyStockRequestHandler
    : IRequestHandler<UpdateInventorySafetyStockRequest, UpdateInventorySafetyStockResponse>
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IProductRepository _productRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化维护仓级安全库存用例处理器
    /// </summary>
    public UpdateInventorySafetyStockRequestHandler(
        IInventoryRepository inventoryRepository,
        IProductRepository productRepository,
        IWarehouseRepository warehouseRepository,
        IAuditLogger auditLogger)
    {
        _inventoryRepository = inventoryRepository;
        _productRepository = productRepository;
        _warehouseRepository = warehouseRepository;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理维护仓级安全库存请求
    /// </summary>
    /// <param name="request">维护请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<UpdateInventorySafetyStockResponse> HandleAsync(
        UpdateInventorySafetyStockRequest request, CancellationToken cancellationToken = default)
    {
        var product = await _productRepository.GetByIdAsync(request.ProductId, cancellationToken);
        if (product is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "商品不存在");
        }

        var warehouse = await _warehouseRepository.GetByIdAsync(request.WarehouseId, cancellationToken);
        if (warehouse is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "仓库不存在");
        }

        var affected = await _inventoryRepository.UpdateSafetyStockAsync(
            request.ProductId, request.WarehouseId, request.SafetyStock, cancellationToken);
        if (affected == 0)
        {
            throw new BusinessException(ErrorCode.NotFound, $"商品 {product.Code} 在该仓库没有库存记录");
        }

        var quantity = await _inventoryRepository.GetQuantityAsync(
            request.ProductId, request.WarehouseId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var changeBuilder = new AuditChangeBuilder()
            .Add("safetyStock", "安全库存", null, AuditSummary.Count(request.SafetyStock));
        await _auditLogger.RecordAsync(new AuditEntry
        {
            Resource = AuditResource.Inventory,
            Action = AuditAction.Update,
            ResourceId = request.ProductId,
            ResourceNo = product.Code,
            Summary = $"设置 {warehouse.Name} 库存安全阈值：{product.Code} {product.Name} = {request.SafetyStock}",
            Changes = changeBuilder.Build(),
            ChangesTruncated = changeBuilder.Truncated,
            UtcNow = now,
        }, cancellationToken);

        return new UpdateInventorySafetyStockResponse
        {
            ProductId = request.ProductId.ToString(),
            WarehouseId = request.WarehouseId.ToString(),
            SafetyStock = request.SafetyStock,
            StockQuantity = quantity,
            IsBelowSafetyStock = request.SafetyStock > 0 && quantity < request.SafetyStock,
        };
    }
}
