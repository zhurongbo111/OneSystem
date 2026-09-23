using App.Core.Abstractions;
using App.Core.Features.Warehouses;

namespace App.Core.Features.StockTakes.GetStockTakePickProducts;

/// <summary>
/// 盘点商品选择用例：IProductRepository.GetPickListAsync（启用商品）叠加
/// IInventoryRepository.GetQuantitiesAsync（**所选仓**账面，038）与
/// IStockMovementRepository.GetProductIdsWithMovementsAsync（**该仓**是否已发生变动，期初模式据此标注「已建账」）。
/// </summary>
public sealed class GetStockTakePickProductsRequestHandler : IRequestHandler<GetStockTakePickProductsRequest, IReadOnlyList<StockTakeProductPickDto>>
{
    private readonly IProductRepository _productRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IStockMovementRepository _stockMovementRepository;

    /// <summary>
    /// 初始化盘点商品选择用例处理器
    /// </summary>
    public GetStockTakePickProductsRequestHandler(
        IProductRepository productRepository,
        IWarehouseRepository warehouseRepository,
        IInventoryRepository inventoryRepository,
        IStockMovementRepository stockMovementRepository)
    {
        _productRepository = productRepository;
        _warehouseRepository = warehouseRepository;
        _inventoryRepository = inventoryRepository;
        _stockMovementRepository = stockMovementRepository;
    }

    /// <summary>
    /// 处理盘点商品选择请求
    /// </summary>
    /// <param name="request">请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<IReadOnlyList<StockTakeProductPickDto>> HandleAsync(
        GetStockTakePickProductsRequest request, CancellationToken cancellationToken = default)
    {
        // 盘点仓解析（038）：入参可空 → 默认仓；账面与「已建账」标记均按该仓计算
        var warehouse = await WarehouseResolver.ResolveAsync(_warehouseRepository, request.WarehouseId, cancellationToken);

        // 启用商品（仓储已过滤启用状态，Handler 不重复过滤）；
        // 038：库存仍按**所选仓**覆盖（下方 GetQuantitiesAsync），故这里不按仓取品项
        var picks = await _productRepository.GetPickListAsync(null, cancellationToken);
        if (picks.Count == 0)
        {
            return Array.Empty<StockTakeProductPickDto>();
        }

        var productIds = picks.Select(p => p.Id).ToList();

        // 该仓账面（批量一次取数，避免逐商品 N+1）与该仓「已发生库存变动」商品集合
        var quantities = await _inventoryRepository.GetQuantitiesAsync(warehouse.Id, productIds, cancellationToken);
        var withMovements = await _stockMovementRepository.GetProductIdsWithMovementsAsync(
            productIds, warehouse.Id, cancellationToken);
        var withMovementsSet = new HashSet<Guid>(withMovements);

        return picks.Select(p => new StockTakeProductPickDto
        {
            Id = p.Id.ToString(),
            Code = p.Code,
            Name = p.Name,
            Unit = p.Unit,
            StockQuantity = quantities.TryGetValue(p.Id, out var quantity) ? quantity : 0,
            HasMovements = withMovementsSet.Contains(p.Id),
        }).ToList();
    }
}
