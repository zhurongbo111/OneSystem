using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.PurchaseOrders.ClosePurchaseOrder;

/// <summary>
/// 关闭采购订单用例：不存在 → 40400；已作废 → 40104；
/// 仅「待收货」/「部分收货」可关闭（已完成 / 已关闭 → 40116，重复关闭幂等由状态判定拦截）。
/// 关闭后保留累计量、不再接受关联入库；不涉及库存与流水。
/// </summary>
public sealed class ClosePurchaseOrderRequestHandler : IRequestHandler<ClosePurchaseOrderRequest, PurchaseOrderDetailDto>
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// 初始化关闭采购订单用例处理器
    /// </summary>
    public ClosePurchaseOrderRequestHandler(
        IPurchaseOrderRepository purchaseOrderRepository,
        ICurrentUser currentUser)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
        _currentUser = currentUser;
    }

    /// <summary>
    /// 处理关闭采购订单请求
    /// </summary>
    /// <param name="request">关闭请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PurchaseOrderDetailDto> HandleAsync(ClosePurchaseOrderRequest request, CancellationToken cancellationToken = default)
    {
        var (order, _) = await _purchaseOrderRepository.GetDetailAsync(request.Id, cancellationToken);
        if (order is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "采购订单不存在");
        }

        if (order.FlowStatus == OrderFlowStatus.Voided)
        {
            throw new BusinessException(ErrorCode.OrderVoided, "订单已作废，禁止再操作");
        }

        if (order.FlowStatus is not (OrderFlowStatus.Pending or OrderFlowStatus.Partial))
        {
            throw new BusinessException(ErrorCode.OrderStateInvalid, "订单当前状态不允许关闭");
        }

        var operatorId = _currentUser.UserId();
        await _purchaseOrderRepository.UpdateFlowStatusAsync(request.Id, OrderFlowStatus.Closed, operatorId, cancellationToken);

        var (updatedOrder, updatedItems) = await _purchaseOrderRepository.GetDetailAsync(request.Id, cancellationToken);
        if (updatedOrder is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "采购订单不存在");
        }

        return PurchaseOrdersDtoMapper.ToPurchaseOrderDetailDto(updatedOrder, updatedItems);
    }
}
