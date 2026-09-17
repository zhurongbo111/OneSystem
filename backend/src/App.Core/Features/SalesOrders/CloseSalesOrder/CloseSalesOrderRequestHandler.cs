using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.SalesOrders.CloseSalesOrder;

/// <summary>
/// 关闭销售订单用例：不存在 → 40400；已作废 → 40104；
/// 仅「待发货」/「部分发货」可关闭（已完成 / 已关闭 → 40116，重复关闭幂等由状态判定拦截）。
/// 关闭后保留累计量、不再接受关联出库；不涉及库存与流水。
/// </summary>
public sealed class CloseSalesOrderRequestHandler : IRequestHandler<CloseSalesOrderRequest, SalesOrderDetailDto>
{
    private readonly ISalesOrderRepository _salesOrderRepository;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// 初始化关闭销售订单用例处理器
    /// </summary>
    public CloseSalesOrderRequestHandler(
        ISalesOrderRepository salesOrderRepository,
        ICurrentUser currentUser)
    {
        _salesOrderRepository = salesOrderRepository;
        _currentUser = currentUser;
    }

    /// <summary>
    /// 处理关闭销售订单请求
    /// </summary>
    /// <param name="request">关闭请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<SalesOrderDetailDto> HandleAsync(CloseSalesOrderRequest request, CancellationToken cancellationToken = default)
    {
        var (order, _) = await _salesOrderRepository.GetDetailAsync(request.Id, cancellationToken);
        if (order is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "销售订单不存在");
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
        await _salesOrderRepository.UpdateFlowStatusAsync(request.Id, OrderFlowStatus.Closed, operatorId, cancellationToken);

        var (updatedOrder, updatedItems) = await _salesOrderRepository.GetDetailAsync(request.Id, cancellationToken);
        if (updatedOrder is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "销售订单不存在");
        }

        return SalesOrdersDtoMapper.ToSalesOrderDetailDto(updatedOrder, updatedItems);
    }
}
