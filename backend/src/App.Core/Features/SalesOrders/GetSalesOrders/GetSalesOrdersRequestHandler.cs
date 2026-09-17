using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.SalesOrders.GetSalesOrders;

/// <summary>
/// 销售订单分页查询用例：仓储分页筛选（含作废订单）→ 映射 DTO（含流转状态与未发数量合计）
/// </summary>
public sealed class GetSalesOrdersRequestHandler : IRequestHandler<GetSalesOrdersRequest, PagedResult<SalesOrderListItemDto>>
{
    private readonly ISalesOrderRepository _salesOrderRepository;

    /// <summary>
    /// 初始化销售订单分页查询用例处理器
    /// </summary>
    public GetSalesOrdersRequestHandler(ISalesOrderRepository salesOrderRepository)
    {
        _salesOrderRepository = salesOrderRepository;
    }

    /// <summary>
    /// 处理销售订单分页查询请求
    /// </summary>
    /// <param name="request">分页查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<SalesOrderListItemDto>> HandleAsync(GetSalesOrdersRequest request, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _salesOrderRepository.GetPagedAsync(
            request.Keyword, request.PartnerId, request.FlowStatus, request.Start, request.End,
            request.Page, request.PageSize, cancellationToken);

        return new PagedResult<SalesOrderListItemDto>
        {
            Items = items.Select(x => SalesOrdersDtoMapper.ToSalesOrderListItemDto(x.Order, x.UnfulfilledQuantity)).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
