using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.PurchaseReceipts.GetPurchaseOrderLines;

/// <summary>
/// 关联订单明细查询用例：订单不存在 → 40400；返回订单头与明细（含未收数量），供入库开单页带出
/// </summary>
public sealed class GetPurchaseOrderLinesRequestHandler : IRequestHandler<GetPurchaseOrderLinesRequest, PurchaseOrderLinesDto>
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;

    /// <summary>
    /// 初始化关联订单明细查询用例处理器
    /// </summary>
    public GetPurchaseOrderLinesRequestHandler(IPurchaseOrderRepository purchaseOrderRepository)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
    }

    /// <summary>
    /// 处理关联订单明细查询请求
    /// </summary>
    /// <param name="request">查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PurchaseOrderLinesDto> HandleAsync(GetPurchaseOrderLinesRequest request, CancellationToken cancellationToken = default)
    {
        var (order, items) = await _purchaseOrderRepository.GetLinesAsync(request.OrderId, cancellationToken);
        if (order is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "采购订单不存在");
        }

        return PurchaseReceiptsDtoMapper.ToPurchaseOrderLinesDto(order, items);
    }
}
