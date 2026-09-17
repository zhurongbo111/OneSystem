using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.PurchaseOrders.GetPurchaseOrders;

/// <summary>
/// 采购订单分页查询用例：仓储分页筛选（含作废订单）→ 映射 DTO（含流转状态与未收数量合计）
/// </summary>
public sealed class GetPurchaseOrdersRequestHandler : IRequestHandler<GetPurchaseOrdersRequest, PagedResult<PurchaseOrderListItemDto>>
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;

    /// <summary>
    /// 初始化采购订单分页查询用例处理器
    /// </summary>
    public GetPurchaseOrdersRequestHandler(IPurchaseOrderRepository purchaseOrderRepository)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
    }

    /// <summary>
    /// 处理采购订单分页查询请求
    /// </summary>
    /// <param name="request">分页查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<PurchaseOrderListItemDto>> HandleAsync(GetPurchaseOrdersRequest request, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _purchaseOrderRepository.GetPagedAsync(
            request.Keyword, request.PartnerId, request.FlowStatus, request.Start, request.End,
            request.Page, request.PageSize, cancellationToken);

        return new PagedResult<PurchaseOrderListItemDto>
        {
            Items = items.Select(x => PurchaseOrdersDtoMapper.ToPurchaseOrderListItemDto(x.Order, x.UnfulfilledQuantity)).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
