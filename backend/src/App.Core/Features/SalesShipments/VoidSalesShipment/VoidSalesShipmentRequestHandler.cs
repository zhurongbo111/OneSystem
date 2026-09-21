using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.SalesShipments.VoidSalesShipment;

/// <summary>
/// 销售出库单作废用例：不存在 → 40400；已作废 → 40104（幂等防重，不重复回冲）；
/// 已核销（`SettledAmount > 0`）→ 40120（作废与核销互斥，须先作废对应收付款单）；
/// 事务内逐行库存回增（IncrementAsync(+quantity)，直接加回）+ 状态置作废 + 审计。
/// **关联订单时**（specs/024-erp-order-flow design.md §3.4）：同步回退订单明细累计已发并重算订单状态
/// （订单已关闭保持关闭、已作废不会出现）；全部在同一事务内完成。
/// 作废后单号不复用；仅改状态，不删数据。
/// </summary>
public sealed class VoidSalesShipmentRequestHandler : IRequestHandler<VoidSalesShipmentRequest, SalesShipmentDetailDto>
{
    private readonly ISalesShipmentRepository _salesShipmentRepository;
    private readonly ISalesOrderRepository _salesOrderRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// 初始化销售出库单作废用例处理器
    /// </summary>
    public VoidSalesShipmentRequestHandler(
        ISalesShipmentRepository salesShipmentRepository,
        ISalesOrderRepository salesOrderRepository,
        IInventoryRepository inventoryRepository,
        IStockMovementRepository stockMovementRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser)
    {
        _salesShipmentRepository = salesShipmentRepository;
        _salesOrderRepository = salesOrderRepository;
        _inventoryRepository = inventoryRepository;
        _stockMovementRepository = stockMovementRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    /// <summary>
    /// 处理销售出库单作废请求
    /// </summary>
    /// <param name="request">作废请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<SalesShipmentDetailDto> HandleAsync(VoidSalesShipmentRequest request, CancellationToken cancellationToken = default)
    {
        var (order, items) = await _salesShipmentRepository.GetDetailAsync(request.Id, cancellationToken);
        if (order is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "销售出库单不存在");
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
                $"销售出库单 {order.ShipmentNo} 已被收付款单核销（已结 {order.SettledAmount:0.00}），请先作废对应收付款单");
        }

        // 关联订单：先取订单当前状态与明细（用于回退累计量与重算状态）
        SalesOrder? linkedOrder = null;
        IReadOnlyDictionary<Guid, SalesOrderItem>? orderItems = null;
        if (order.OrderId is not null)
        {
            var (found, lines) = await _salesOrderRepository.GetDetailAsync(order.OrderId.Value, cancellationToken);
            linkedOrder = found;
            orderItems = found is null ? null : lines.ToDictionary(i => i.Id);
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();

        // 状态流转判定在 Handler（design.md §3.4）：回冲与状态变更同一事务
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // 回冲：逐行库存 += 数量（与销售扣减同事务；直接加回，无前置校验，见 design.md §5 决策）
            foreach (var item in items)
            {
                await _inventoryRepository.IncrementAsync(item.ProductId, item.Quantity, cancellationToken);

                // 成本：冲销还原 —— 复用原出库流水的成本单价（erp-cost design §0.2），保证「出 + 冲回 = 0」
                var unitCost = await _stockMovementRepository.GetMovementUnitCostAsync(
                    order.Id, item.ProductId, StockMovementType.SalesOutbound, cancellationToken) ?? 0m;
                var totalCost = CostCalculator.TotalCost(item.Quantity, unitCost);
                await _inventoryRepository.ApplyInboundCostAsync(item.ProductId, item.Quantity, unitCost, cancellationToken);

                // 库存流水：销售作废回增，与库存增减同事务（erp-stock-movement design §3.7）
                await _stockMovementRepository.AppendAsync(new StockMovement
                {
                    Id = Guid.NewGuid(),
                    ProductId = item.ProductId,
                    MovementType = StockMovementType.SalesVoid,
                    Quantity = item.Quantity,
                    UnitCost = unitCost,
                    TotalCost = totalCost,
                    SourceId = order.Id,
                    SourceNo = order.ShipmentNo,
                    CreatedAt = now,
                    CreatedBy = operatorId,
                }, cancellationToken);
            }

            if (linkedOrder is not null && orderItems is not null)
            {
                // 回退订单明细累计已发
                var rolledBack = new Dictionary<Guid, int>();
                foreach (var item in items)
                {
                    if (item.OrderItemId is null)
                    {
                        continue;
                    }

                    var key = item.OrderItemId.Value;
                    rolledBack[key] = rolledBack.TryGetValue(key, out var accumulated) ? accumulated + item.Quantity : item.Quantity;
                    await _salesOrderRepository.AddFulfilledQuantityAsync(key, -item.Quantity, cancellationToken);
                }

                // 状态重算：仅当订单当前为部分发货 / 已完成（已关闭是人工决策、保持关闭；已作废不会出现）
                if (linkedOrder.FlowStatus is OrderFlowStatus.Partial or OrderFlowStatus.Completed)
                {
                    var allRolledBack = orderItems.Values.All(i =>
                        i.FulfilledQuantity - (rolledBack.TryGetValue(i.Id, out var quantity) ? quantity : 0) <= 0);
                    var flowStatus = allRolledBack ? OrderFlowStatus.Pending : OrderFlowStatus.Partial;
                    await _salesOrderRepository.UpdateFlowStatusAsync(linkedOrder.Id, flowStatus, operatorId, cancellationToken);
                }
            }

            await _salesShipmentRepository.UpdateStatusAsync(request.Id, OrderStatus.Voided, operatorId, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        var (updatedOrder, updatedItems) = await _salesShipmentRepository.GetDetailAsync(request.Id, cancellationToken);
        if (updatedOrder is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "销售出库单不存在");
        }

        return SalesShipmentsDtoMapper.ToSalesShipmentDetailDto(updatedOrder, updatedItems);
    }
}
