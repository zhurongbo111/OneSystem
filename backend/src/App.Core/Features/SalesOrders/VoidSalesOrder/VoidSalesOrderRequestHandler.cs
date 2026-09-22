using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.SalesOrders.VoidSalesOrder;

/// <summary>
/// 销售订单作废用例：不存在 → 40400；已作废 → 40104（幂等防重）；
/// 已开始发货（部分发货 / 已完成）后不可作废（应通过出库单作废或关闭订单处理）→ 40116。
/// 订单是计划数据，作废不涉及库存与流水；作废后单号不复用。
/// </summary>
public sealed class VoidSalesOrderRequestHandler : IRequestHandler<VoidSalesOrderRequest, SalesOrderDetailDto>
{
    private readonly ISalesOrderRepository _salesOrderRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化销售订单作废用例处理器
    /// </summary>
    public VoidSalesOrderRequestHandler(
        ISalesOrderRepository salesOrderRepository,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _salesOrderRepository = salesOrderRepository;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理销售订单作废请求
    /// </summary>
    /// <param name="request">作废请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<SalesOrderDetailDto> HandleAsync(VoidSalesOrderRequest request, CancellationToken cancellationToken = default)
    {
        var (order, _) = await _salesOrderRepository.GetDetailAsync(request.Id, cancellationToken);
        if (order is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "销售订单不存在");
        }

        if (order.FlowStatus == OrderFlowStatus.Voided)
        {
            // 已作废禁止再操作（防重复作废）
            throw new BusinessException(ErrorCode.OrderVoided, "订单已作废，禁止再操作");
        }

        // 仅未开始发货可作废；已发生的发货需通过出库单作废处理（design.md §5）
        if (order.FlowStatus != OrderFlowStatus.Pending)
        {
            throw new BusinessException(ErrorCode.OrderStateInvalid, "订单当前状态不允许作废");
        }

        var beforeFlowStatus = order.FlowStatus;
        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();
        await _salesOrderRepository.UpdateFlowStatusAsync(request.Id, OrderFlowStatus.Voided, operatorId, cancellationToken);

        var voidedOrderChangeBuilder = new AuditChangeBuilder()
            .Add("flowStatus", "订单状态", AuditText.OrderFlowStatus(beforeFlowStatus, isPurchase: false), AuditText.OrderFlowStatus(OrderFlowStatus.Voided, isPurchase: false));
        await _auditLogger.RecordAsync(new AuditEntry
        {
            Resource = AuditResource.SalesOrder,
            Action = AuditAction.Void,
            ResourceId = order.Id,
            ResourceNo = order.OrderNo,
            Summary = $"作废销售订单 {order.OrderNo}（客户：{order.PartnerName}、{AuditSummary.Money(order.TotalAmount)}）",
            Changes = voidedOrderChangeBuilder.Build(),
            ChangesTruncated = voidedOrderChangeBuilder.Truncated,
            UtcNow = now,
        }, cancellationToken);

        var (updatedOrder, updatedItems) = await _salesOrderRepository.GetDetailAsync(request.Id, cancellationToken);
        if (updatedOrder is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "销售订单不存在");
        }

        return SalesOrdersDtoMapper.ToSalesOrderDetailDto(updatedOrder, updatedItems);
    }
}
