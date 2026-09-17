using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.PurchaseReceipts.GetPurchaseReceipts;

/// <summary>
/// 采购单分页查询用例：仓储分页筛选（含作废单据）→ 映射 DTO（含 Status 供前端置灰）
/// </summary>
public sealed class GetPurchaseReceiptsRequestHandler : IRequestHandler<GetPurchaseReceiptsRequest, PagedResult<PurchaseReceiptListItemDto>>
{
    private readonly IPurchaseReceiptRepository _purchaseReceiptRepository;

    /// <summary>
    /// 初始化采购单分页查询用例处理器
    /// </summary>
    public GetPurchaseReceiptsRequestHandler(IPurchaseReceiptRepository purchaseReceiptRepository)
    {
        _purchaseReceiptRepository = purchaseReceiptRepository;
    }

    /// <summary>
    /// 处理采购单分页查询请求
    /// </summary>
    /// <param name="request">分页查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<PurchaseReceiptListItemDto>> HandleAsync(GetPurchaseReceiptsRequest request, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _purchaseReceiptRepository.GetPagedAsync(
            request.Keyword, request.PartnerId, request.OrderId, request.Start, request.End, request.SettlementState,
            request.Page, request.PageSize, cancellationToken);

        return new PagedResult<PurchaseReceiptListItemDto>
        {
            Items = items.Select(PurchaseReceiptsDtoMapper.ToPurchaseReceiptListItemDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
