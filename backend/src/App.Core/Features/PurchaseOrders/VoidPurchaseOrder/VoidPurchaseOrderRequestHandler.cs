using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.PurchaseOrders.VoidPurchaseOrder;

/// <summary>
/// 采购订单作废用例：不存在 → 40400；已作废 → 40104（幂等防重）；
/// 已开始收货（部分收货 / 已完成）后不可作废（应通过入库单作废或关闭订单处理）→ 40116。
/// 订单是计划数据，作废不涉及库存与流水；作废后单号不复用。
/// </summary>
public sealed class VoidPurchaseOrderRequestHandler : IRequestHandler<VoidPurchaseOrderRequest, PurchaseOrderDetailDto>
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化采购订单作废用例处理器
    /// </summary>
    public VoidPurchaseOrderRequestHandler(
        IPurchaseOrderRepository purchaseOrderRepository,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理采购订单作废请求
    /// </summary>
    /// <param name="request">作废请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PurchaseOrderDetailDto> HandleAsync(VoidPurchaseOrderRequest request, CancellationToken cancellationToken = default)
    {
        var (order, _) = await _purchaseOrderRepository.GetDetailAsync(request.Id, cancellationToken);
        if (order is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "采购订单不存在");
        }

        if (order.FlowStatus == OrderFlowStatus.Voided)
        {
            // 已作废禁止再操作（防重复作废）
            throw new BusinessException(ErrorCode.OrderVoided, "订单已作废，禁止再操作");
        }

        // 仅未开始收货可作废；已发生的收货需通过入库单作废处理（design.md §5）
        if (order.FlowStatus != OrderFlowStatus.Pending)
        {
            throw new BusinessException(ErrorCode.OrderStateInvalid, "订单当前状态不允许作废");
        }

        var beforeFlowStatus = order.FlowStatus;
        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();
        await _purchaseOrderRepository.UpdateFlowStatusAsync(request.Id, OrderFlowStatus.Voided, operatorId, cancellationToken);

        var voidedOrderChangeBuilder = new AuditChangeBuilder()
            .Add("flowStatus", "订单状态", AuditText.OrderFlowStatus(beforeFlowStatus), AuditText.OrderFlowStatus(OrderFlowStatus.Voided));
        await _auditLogger.RecordAsync(new AuditEntry
        {
            Resource = AuditResource.PurchaseOrder,
            Action = AuditAction.Void,
            ResourceId = order.Id,
            ResourceNo = order.OrderNo,
            Summary = $"作废采购订单 {order.OrderNo}（供应商：{order.PartnerName}、{AuditSummary.Money(order.TotalAmount)}）",
            Changes = voidedOrderChangeBuilder.Build(),
            ChangesTruncated = voidedOrderChangeBuilder.Truncated,
            UtcNow = now,
        }, cancellationToken);

        var (updatedOrder, updatedItems) = await _purchaseOrderRepository.GetDetailAsync(request.Id, cancellationToken);
        if (updatedOrder is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "采购订单不存在");
        }

        return PurchaseOrdersDtoMapper.ToPurchaseOrderDetailDto(updatedOrder, updatedItems);
    }
}
