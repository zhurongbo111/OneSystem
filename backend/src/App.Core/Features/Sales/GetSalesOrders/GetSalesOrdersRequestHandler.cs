using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.Sales.GetSalesOrders;

/// <summary>
/// 销售单分页查询用例：仓储分页筛选（含作废单据）→ 映射 DTO（含 Status 供前端置灰）
/// </summary>
public sealed class GetSalesOrdersRequestHandler : IRequestHandler<GetSalesOrdersRequest, PagedResult<SalesOrderListItemDto>>
{
    private readonly ISalesOrderRepository _salesOrderRepository;

    /// <summary>
    /// 初始化销售单分页查询用例处理器
    /// </summary>
    public GetSalesOrdersRequestHandler(ISalesOrderRepository salesOrderRepository)
    {
        _salesOrderRepository = salesOrderRepository;
    }

    /// <summary>
    /// 处理销售单分页查询请求
    /// </summary>
    /// <param name="request">分页查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<SalesOrderListItemDto>> HandleAsync(GetSalesOrdersRequest request, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _salesOrderRepository.GetPagedAsync(
            request.Keyword, request.PartnerId, request.Start, request.End, request.Settlement,
            request.Page, request.PageSize, cancellationToken);

        return new PagedResult<SalesOrderListItemDto>
        {
            Items = items.Select(o => new SalesOrderListItemDto
            {
                Id = o.Id.ToString(),
                OrderNo = o.OrderNo,
                PartnerId = o.PartnerId.ToString(),
                PartnerName = o.PartnerName,
                OrderDate = o.OrderDate,
                TotalAmount = o.TotalAmount,
                SettlementStatus = (int)o.SettlementStatus,
                Status = (int)o.Status,
                CreatedAt = o.CreatedAt,
            }).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
