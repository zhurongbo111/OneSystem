using App.Core.Abstractions;

namespace App.Core.Features.StockTakes.GetStockTakePickProducts;

/// <summary>
/// 盘点商品选择用例：IProductRepository.GetPickListAsync（启用商品 + 当前库存，已过滤）
/// 叠加 IStockMovementRepository.GetProductIdsWithMovementsAsync 得到 hasMovements 标记（期初模式据此标注「已建账」）。
/// </summary>
public sealed class GetStockTakePickProductsRequestHandler : IRequestHandler<GetStockTakePickProductsRequest, IReadOnlyList<StockTakeProductPickDto>>
{
    private readonly IProductRepository _productRepository;
    private readonly IStockMovementRepository _stockMovementRepository;

    /// <summary>
    /// 初始化盘点商品选择用例处理器
    /// </summary>
    public GetStockTakePickProductsRequestHandler(
        IProductRepository productRepository,
        IStockMovementRepository stockMovementRepository)
    {
        _productRepository = productRepository;
        _stockMovementRepository = stockMovementRepository;
    }

    /// <summary>
    /// 处理盘点商品选择请求
    /// </summary>
    /// <param name="request">请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<IReadOnlyList<StockTakeProductPickDto>> HandleAsync(GetStockTakePickProductsRequest request, CancellationToken cancellationToken = default)
    {
        // 启用商品 + 当前库存（仓储已过滤启用状态，Handler 不重复过滤）
        var picks = await _productRepository.GetPickListAsync(cancellationToken);
        if (picks.Count == 0)
        {
            return Array.Empty<StockTakeProductPickDto>();
        }

        // 批量标注「是否已发生库存变动」（期初模式据此禁用已建账商品）
        var productIds = picks.Select(p => p.Id).ToList();
        var withMovements = await _stockMovementRepository.GetProductIdsWithMovementsAsync(productIds, cancellationToken);
        var withMovementsSet = new HashSet<Guid>(withMovements);

        return picks.Select(p => new StockTakeProductPickDto
        {
            Id = p.Id.ToString(),
            Code = p.Code,
            Name = p.Name,
            Unit = p.Unit,
            StockQuantity = p.StockQuantity,
            HasMovements = withMovementsSet.Contains(p.Id),
        }).ToList();
    }
}
