using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.StockTakes.GetStockTakes;

/// <summary>
/// 盘点单分页查询用例：仓储分页筛选（单号关键词 / 类型 / 盘点日期闭区间，CreatedAt DESC）→ 映射 DTO
/// </summary>
public sealed class GetStockTakesRequestHandler : IRequestHandler<GetStockTakesRequest, PagedResult<StockTakeListItemDto>>
{
    private readonly IStockTakeRepository _stockTakeRepository;

    /// <summary>
    /// 初始化盘点单分页查询用例处理器
    /// </summary>
    public GetStockTakesRequestHandler(IStockTakeRepository stockTakeRepository)
    {
        _stockTakeRepository = stockTakeRepository;
    }

    /// <summary>
    /// 处理盘点单分页查询请求
    /// </summary>
    /// <param name="request">分页查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<StockTakeListItemDto>> HandleAsync(GetStockTakesRequest request, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _stockTakeRepository.GetPagedAsync(
            request.Keyword, request.Type, request.Start, request.End,
            request.Page, request.PageSize, cancellationToken);

        return new PagedResult<StockTakeListItemDto>
        {
            Items = items.Select(StockTakeDtoMapper.ToStockTakeListItemDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
