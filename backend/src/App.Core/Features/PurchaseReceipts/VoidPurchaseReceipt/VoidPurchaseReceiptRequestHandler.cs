using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.PurchaseReceipts.VoidPurchaseReceipt;

/// <summary>
/// 采购入库单作废用例：不存在 → 40400；已作废 → 40104（幂等防重，不重复回冲）；
/// 已核销（`SettledAmount > 0`）→ 40120（作废与核销互斥，须先作废对应收付款单）；
/// 事务内逐行库存回冲（IncrementAsync(-quantity)，允许冲负）+ 状态置作废 + 审计。
/// **关联订单时**（specs/024-erp-order-flow design.md §3.4）：同步回退订单明细累计已收并重算订单状态
/// （订单已关闭保持关闭、已作废不会出现）；全部在同一事务内完成。
/// 作废后单号不复用；仅改状态，不删数据。
/// </summary>
public sealed class VoidPurchaseReceiptRequestHandler : IRequestHandler<VoidPurchaseReceiptRequest, PurchaseReceiptDetailDto>
{
    private readonly IPurchaseReceiptRepository _purchaseReceiptRepository;
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化采购入库单作废用例处理器
    /// </summary>
    public VoidPurchaseReceiptRequestHandler(
        IPurchaseReceiptRepository purchaseReceiptRepository,
        IPurchaseOrderRepository purchaseOrderRepository,
        IInventoryRepository inventoryRepository,
        IStockMovementRepository stockMovementRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _purchaseReceiptRepository = purchaseReceiptRepository;
        _purchaseOrderRepository = purchaseOrderRepository;
        _inventoryRepository = inventoryRepository;
        _stockMovementRepository = stockMovementRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理采购入库单作废请求
    /// </summary>
    /// <param name="request">作废请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PurchaseReceiptDetailDto> HandleAsync(VoidPurchaseReceiptRequest request, CancellationToken cancellationToken = default)
    {
        var (order, items) = await _purchaseReceiptRepository.GetDetailAsync(request.Id, cancellationToken: cancellationToken);
        if (order is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "采购入库单不存在");
        }

        if (order.Status == OrderStatus.Voided)
        {
            // 已作废禁止再操作（防重复作废 / 重复回冲）
            throw new BusinessException(ErrorCode.OrderVoided, "单据已作废，禁止再操作");
        }

        // 已核销禁止作废：作废与核销互斥（specs/023-erp-settlement design.md §0）——须先作废对应收付款单回退已结金额
        if (order.SettledAmount > 0)
        {
            throw new BusinessException(
                ErrorCode.OrderSettledCannotVoid,
                $"采购入库单 {order.ReceiptNo} 已被收付款单核销（已结 {order.SettledAmount:0.00}），请先作废对应收付款单");
        }

        // 关联订单：先取订单当前状态与明细（用于回退累计量与重算状态）
        PurchaseOrder? linkedOrder = null;
        IReadOnlyDictionary<Guid, PurchaseOrderItem>? orderItems = null;
        if (order.OrderId is not null)
        {
            var (found, lines) = await _purchaseOrderRepository.GetDetailAsync(order.OrderId.Value, cancellationToken);
            linkedOrder = found;
            orderItems = found is null ? null : lines.ToDictionary(i => i.Id);
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();

        // 状态流转判定在 Handler（design.md §3.4）：回冲与状态变更同一事务
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // 回冲：逐行库存 -= 数量（与入库同一事务；允许冲负，见 design.md §5 决策）
            foreach (var item in items)
            {
                await _inventoryRepository.IncrementAsync(item.ProductId, -item.Quantity, cancellationToken);

                // 成本：冲销还原 —— 复用原入库流水的成本单价（erp-cost design §0.2），保证「入 + 冲回 = 0」；
                // 查不到原流水（历史数据）按 0 计，重算用例会统计缺价
                var unitCost = await _stockMovementRepository.GetMovementUnitCostAsync(
                    order.Id, item.ProductId, StockMovementType.PurchaseInbound, cancellationToken) ?? 0m;
                var totalCost = CostCalculator.TotalCost(item.Quantity, unitCost);
                await _inventoryRepository.ApplyOutboundCostAsync(item.ProductId, totalCost, cancellationToken);

                // 库存流水：采购作废回冲，与库存增减同事务（erp-stock-movement design §3.7）
                await _stockMovementRepository.AppendAsync(new StockMovement
                {
                    Id = Guid.NewGuid(),
                    ProductId = item.ProductId,
                    MovementType = StockMovementType.PurchaseVoid,
                    Quantity = -item.Quantity,
                    UnitCost = unitCost,
                    TotalCost = -totalCost,
                    SourceId = order.Id,
                    SourceNo = order.ReceiptNo,
                    CreatedAt = now,
                    CreatedBy = operatorId,
                }, cancellationToken);
            }

            if (linkedOrder is not null && orderItems is not null)
            {
                // 回退订单明细累计已收
                var rolledBack = new Dictionary<Guid, int>();
                foreach (var item in items)
                {
                    if (item.OrderItemId is null)
                    {
                        continue;
                    }

                    var key = item.OrderItemId.Value;
                    rolledBack[key] = rolledBack.TryGetValue(key, out var accumulated) ? accumulated + item.Quantity : item.Quantity;
                    await _purchaseOrderRepository.AddFulfilledQuantityAsync(key, -item.Quantity, cancellationToken);
                }

                // 状态重算：仅当订单当前为部分收货 / 已完成（已关闭是人工决策、保持关闭；已作废不会出现）
                if (linkedOrder.FlowStatus is OrderFlowStatus.Partial or OrderFlowStatus.Completed)
                {
                    var allRolledBack = orderItems.Values.All(i =>
                        i.FulfilledQuantity - (rolledBack.TryGetValue(i.Id, out var quantity) ? quantity : 0) <= 0);
                    var flowStatus = allRolledBack ? OrderFlowStatus.Pending : OrderFlowStatus.Partial;
                    await _purchaseOrderRepository.UpdateFlowStatusAsync(linkedOrder.Id, flowStatus, operatorId, cancellationToken);
                }
            }

            await _purchaseReceiptRepository.UpdateStatusAsync(request.Id, OrderStatus.Voided, operatorId, cancellationToken);

            // 业务写成功后、提交前追加操作日志：与业务同事务，异常回滚则不产生日志
            var voidedReceiptChangeBuilder = new AuditChangeBuilder()
                .Add("status", "单据状态", AuditText.OrderStatus(OrderStatus.Normal), AuditText.OrderStatus(OrderStatus.Voided));
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.PurchaseReceipt,
                Action = AuditAction.Void,
                ResourceId = order.Id,
                ResourceNo = order.ReceiptNo,
                Summary = $"作废采购入库单 {order.ReceiptNo}（供应商：{order.PartnerName}、{AuditSummary.Money(order.TotalAmount)}）",
                Changes = voidedReceiptChangeBuilder.Build(),
                ChangesTruncated = voidedReceiptChangeBuilder.Truncated,
                UtcNow = now,
            }, cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        var (updatedOrder, updatedItems) = await _purchaseReceiptRepository.GetDetailAsync(request.Id, cancellationToken: cancellationToken);
        if (updatedOrder is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "采购入库单不存在");
        }

        return PurchaseReceiptsDtoMapper.ToPurchaseReceiptDetailDto(updatedOrder, updatedItems);
    }
}
