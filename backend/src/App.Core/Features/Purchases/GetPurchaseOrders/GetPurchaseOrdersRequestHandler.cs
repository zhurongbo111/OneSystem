using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.Purchases.GetPurchaseOrders;

/// <summary>
/// 采购单分页查询用例：仓储分页筛选（含作废单据）→ 映射 DTO（含 Status 供前端置灰）
/// </summary>
public sealed class GetPurchaseOrdersRequestHandler : IRequestHandler<GetPurchaseOrdersRequest, PagedResult<PurchaseOrderListItemDto>>
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;

    /// <summary>
    /// 初始化采购单分页查询用例处理器
    /// </summary>
    public GetPurchaseOrdersRequestHandler(IPurchaseOrderRepository purchaseOrderRepository)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
    }

    /// <summary>
    /// 处理采购单分页查询请求
    /// </summary>
    /// <param name="request">分页查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<PurchaseOrderListItemDto>> HandleAsync(GetPurchaseOrdersRequest request, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _purchaseOrderRepository.GetPagedAsync(
            request.Keyword, request.PartnerId, request.Start, request.End, request.Settlement,
            request.Page, request.PageSize, cancellationToken);

        return new PagedResult<PurchaseOrderListItemDto>
        {
            Items = items.Select(o => new PurchaseOrderListItemDto
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
