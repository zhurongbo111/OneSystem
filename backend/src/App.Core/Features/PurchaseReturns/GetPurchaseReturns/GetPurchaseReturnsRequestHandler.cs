using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.PurchaseReturns.GetPurchaseReturns;

/// <summary>
/// 采购退货单分页查询用例：仓储分页筛选（含作废单据）→ 映射 DTO（含 Status 供前端置灰）
/// </summary>
public sealed class GetPurchaseReturnsRequestHandler : IRequestHandler<GetPurchaseReturnsRequest, PagedResult<PurchaseReturnListItemDto>>
{
    private readonly IPurchaseReturnRepository _purchaseReturnRepository;

    /// <summary>
    /// 初始化采购退货单分页查询用例处理器
    /// </summary>
    public GetPurchaseReturnsRequestHandler(IPurchaseReturnRepository purchaseReturnRepository)
    {
        _purchaseReturnRepository = purchaseReturnRepository;
    }

    /// <summary>
    /// 处理采购退货单分页查询请求
    /// </summary>
    /// <param name="request">分页查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<PurchaseReturnListItemDto>> HandleAsync(GetPurchaseReturnsRequest request, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _purchaseReturnRepository.GetPagedAsync(
            request.Keyword, request.PartnerId, request.Start, request.End, request.SettlementState,
            request.Page, request.PageSize, cancellationToken);

        return new PagedResult<PurchaseReturnListItemDto>
        {
            Items = items.Select(PurchaseReturnsDtoMapper.ToPurchaseReturnListItemDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
