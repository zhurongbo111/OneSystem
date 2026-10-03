using App.Core.Abstractions;
using App.Core.Features.Warehouses;

namespace App.Core.Features.StockTakes.GetStockTakePickProducts;

/// <summary>
/// 盘点商品选择用例：IProductRepository.GetPickListAsync（启用商品）叠加
/// IInventoryRepository.GetQuantitiesAsync（**所选仓**账面，038）与
/// IStockMovementRepository.GetProductIdsWithMovementsAsync（**该仓**是否已发生变动，期初模式据此标注「已建账」）。
/// 040：按批次商品另带出该仓批次行（批次档案 + 批次账面 + 批次是否已建账），供前端按批次拆行。
/// </summary>
public sealed class GetStockTakePickProductsRequestHandler : IRequestHandler<GetStockTakePickProductsRequest, IReadOnlyList<StockTakeProductPickDto>>
{
    private readonly IProductRepository _productRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IBatchRepository _batchRepository;
    private readonly ISystemClock _clock;

    /// <summary>
    /// 初始化盘点商品选择用例处理器
    /// </summary>
    public GetStockTakePickProductsRequestHandler(
        IProductRepository productRepository,
        IWarehouseRepository warehouseRepository,
        IInventoryRepository inventoryRepository,
        IStockMovementRepository stockMovementRepository,
        IBatchRepository batchRepository,
        ISystemClock clock)
    {
        _productRepository = productRepository;
        _warehouseRepository = warehouseRepository;
        _inventoryRepository = inventoryRepository;
        _stockMovementRepository = stockMovementRepository;
        _batchRepository = batchRepository;
        _clock = clock;
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

        // 040 按批次盘点：按批次商品带出该仓批次行（批次档案 + 批次账面 + 批次是否已建账）；
        // 批次行库存取自批次行（非批次行库存恒 0），商品级 StockQuantity / HasMovements 仅对非批次商品有意义
        var today = _clock.Today;
        var batchPicks = new Dictionary<Guid, IReadOnlyList<StockTakeBatchPickDto>>();
        foreach (var p in picks.Where(p => p.IsBatchManaged))
        {
            var batchItems = await _batchRepository.GetPickListAsync(p.Id, warehouse.Id, today, cancellationToken);
            if (batchItems.Count == 0)
            {
                continue;
            }

            var batchIds = batchItems.Select(b => b.BatchId).ToList();
            var batchWithMovements = await _stockMovementRepository.GetBatchIdsWithMovementsAsync(
                batchIds, warehouse.Id, cancellationToken);
            var batchWithMovementsSet = new HashSet<Guid>(batchWithMovements);
            var batchQuantities = await _inventoryRepository.GetBatchQuantitiesAsync(warehouse.Id, p.Id, cancellationToken);

            batchPicks[p.Id] = batchItems
                .Select(b => new StockTakeBatchPickDto
                {
                    Id = b.BatchId.ToString(),
                    BatchNo = b.BatchNo,
                    StockQuantity = batchQuantities.TryGetValue(b.BatchId, out var bq) ? bq : 0,
                    HasMovements = batchWithMovementsSet.Contains(b.BatchId),
                })
                .ToList();
        }

        return picks.Select(p => new StockTakeProductPickDto
        {
            Id = p.Id.ToString(),
            Code = p.Code,
            Name = p.Name,
            Unit = p.Unit,
            IsBatchManaged = p.IsBatchManaged,
            StockQuantity = p.IsBatchManaged ? 0 : (quantities.TryGetValue(p.Id, out var quantity) ? quantity : 0),
            HasMovements = p.IsBatchManaged ? false : withMovementsSet.Contains(p.Id),
            Batches = batchPicks.GetValueOrDefault(p.Id),
        }).ToList();
    }
}
