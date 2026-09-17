using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.StockMovements.GetStockMovements;

/// <summary>
/// 库存流水分页查询用例：按单号关键词 / 商品 / 变动类型 / 变动时间范围筛选，变动时间倒序（只读）
/// </summary>
public sealed class GetStockMovementsRequestHandler : IRequestHandler<GetStockMovementsRequest, PagedResult<StockMovementListItemDto>>
{
    private readonly IStockMovementRepository _stockMovementRepository;

    /// <summary>
    /// 初始化库存流水查询用例处理器
    /// </summary>
    public GetStockMovementsRequestHandler(IStockMovementRepository stockMovementRepository)
    {
        _stockMovementRepository = stockMovementRepository;
    }

    /// <summary>
    /// 处理库存流水查询请求
    /// </summary>
    /// <param name="request">查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<StockMovementListItemDto>> HandleAsync(GetStockMovementsRequest request, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _stockMovementRepository.GetPagedAsync(
            request.Keyword,
            request.ProductId,
            request.Type,
            request.Start,
            request.End,
            request.Page,
            request.PageSize,
            cancellationToken);

        return new PagedResult<StockMovementListItemDto>
        {
            Items = items.Select(StockMovementsDtoMapper.ToStockMovementListItemDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
