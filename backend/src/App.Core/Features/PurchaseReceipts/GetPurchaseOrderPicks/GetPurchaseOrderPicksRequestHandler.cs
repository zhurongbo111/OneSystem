using App.Core.Abstractions;

namespace App.Core.Features.PurchaseReceipts.GetPurchaseOrderPicks;

/// <summary>
/// 可关联采购订单候选查询用例：按供应商取待收货 / 部分收货订单（创建时间倒序），供入库开单页下拉
/// </summary>
public sealed class GetPurchaseOrderPicksRequestHandler : IRequestHandler<GetPurchaseOrderPicksRequest, IReadOnlyList<PurchaseOrderPickDto>>
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;

    /// <summary>
    /// 初始化可关联订单候选查询用例处理器
    /// </summary>
    public GetPurchaseOrderPicksRequestHandler(IPurchaseOrderRepository purchaseOrderRepository)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
    }

    /// <summary>
    /// 处理可关联订单候选查询请求
    /// </summary>
    /// <param name="request">查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<IReadOnlyList<PurchaseOrderPickDto>> HandleAsync(GetPurchaseOrderPicksRequest request, CancellationToken cancellationToken = default)
    {
        var picks = await _purchaseOrderRepository.GetPicksAsync(request.PartnerId, cancellationToken);
        return picks.Select(PurchaseReceiptsDtoMapper.ToPurchaseOrderPickDto).ToList();
    }
}
