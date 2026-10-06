using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Finance;

namespace App.Core.Features.SalesShipments;

/// <summary>
/// 销售出库单「生效」组件（specs/042-erp-approval/design.md §3.1）：把「这张单生效」这件事收敛到一处，
/// 由**未命中审批规则的创建路径**（specs/016-erp-sale）与**审批通过路径**（specs/042-erp-approval）共用。
/// 生效内容与既有完全一致：按仓 / 按批次条件扣减库存（不足 → 40103）+ 成本结转 + 库存流水 +
/// 关联销售订单累计回写 + 自动凭证（含成本结转分录）。
/// **不自行 Commit**：事务由调用方 <see cref="IUnitOfWork"/> 控制。
/// </summary>
public sealed class SalesShipmentFulfillment
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly ISalesOrderRepository _salesOrderRepository;
    private readonly IVoucherRepository _voucherRepository;
    private readonly IAccountMappingRepository _accountMappingRepository;
    private readonly IAccountingPeriodRepository _accountingPeriodRepository;
    private readonly IAccountRepository _accountRepository;

    /// <summary>
    /// 初始化销售出库单生效组件
    /// </summary>
    public SalesShipmentFulfillment(
        IInventoryRepository inventoryRepository,
        IStockMovementRepository stockMovementRepository,
        ISalesOrderRepository salesOrderRepository,
        IVoucherRepository voucherRepository,
        IAccountMappingRepository accountMappingRepository,
        IAccountingPeriodRepository accountingPeriodRepository,
        IAccountRepository accountRepository)
    {
        _inventoryRepository = inventoryRepository;
        _stockMovementRepository = stockMovementRepository;
        _salesOrderRepository = salesOrderRepository;
        _voucherRepository = voucherRepository;
        _accountMappingRepository = accountMappingRepository;
        _accountingPeriodRepository = accountingPeriodRepository;
        _accountRepository = accountRepository;
    }

    /// <summary>
    /// 执行销售出库单生效（条件扣减库存 + 成本结转 + 流水 + 订单累计回写 + 自动凭证）；
    /// 任一行库存不足 → 抛 40103，由调用方回滚整单（审批通过时保持「待审批」可重试）
    /// </summary>
    /// <param name="order">销售出库单主表（已持久化，含仓 / 金额 / 单号快照）</param>
    /// <param name="items">明细行（批次已在创建时解析并落库）</param>
    /// <param name="operatorId">操作人 id（由调用方取 ICurrentUser 传入，可空）</param>
    /// <param name="utcNow">操作时间（由调用方注入，便于单测）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task ApplyAsync(
        SalesShipment order,
        IReadOnlyList<SalesShipmentItem> items,
        Guid? operatorId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        // 关联订单：先取当前状态与明细（用于按「本次发货后的累计已发量」推导订单流转状态）
        SalesOrder? linkedOrder = null;
        IReadOnlyList<SalesOrderItem> orderItems = Array.Empty<SalesOrderItem>();
        if (order.OrderId is not null)
        {
            var (found, lines) = await _salesOrderRepository.GetDetailAsync(order.OrderId.Value, cancellationToken);
            linkedOrder = found;
            orderItems = lines;
        }

        // 成本：出库按「变动前」该仓该批次行移动加权均价结转（erp-cost design §0.2；040 起按批次行）—— 必须在扣减前读取
        var outboundUnitCosts = new List<decimal>(items.Count);
        foreach (var item in items)
        {
            outboundUnitCosts.Add(
                await _inventoryRepository.GetAverageCostAsync(item.ProductId, order.WarehouseId, item.BatchId, cancellationToken));
        }

        // 逐行按仓按批次扣减：任一行该批次行库存不足 → 抛 40103（message 含仓名 / 批次号）
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var ok = await _inventoryRepository.TryDecrementAsync(
                item.ProductId, order.WarehouseId, item.BatchId, item.Quantity, cancellationToken);
            if (!ok)
            {
                var current = await _inventoryRepository.GetQuantityAsync(
                    item.ProductId, order.WarehouseId, item.BatchId, cancellationToken);
                throw new BusinessException(
                    ErrorCode.InsufficientStock,
                    $"库存不足：{order.WarehouseName} 商品 {item.ProductName}"
                    + (item.BatchNo is not null ? $" 批次 {item.BatchNo}" : string.Empty)
                    + $"（当前 {current}，需要 {item.Quantity}）");
            }
        }

        // 库存流水：销售出库，与库存扣减同事务；成本结转金额 = Σ 本次出库流水成本（自动凭证的成本结转分录金额）
        var costAmount = 0m;
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];

            // 成本：按变动前批次行均价结转（均价不变 —— 按均价出库不改变均值；040 起按批次行）
            var unitCost = outboundUnitCosts[index];
            var totalCost = CostCalculator.TotalCost(item.Quantity, unitCost);
            costAmount += totalCost;
            await _inventoryRepository.ApplyOutboundCostAsync(
                item.ProductId, order.WarehouseId, item.BatchId, totalCost, cancellationToken);

            await _stockMovementRepository.AppendAsync(new StockMovement
            {
                Id = Guid.NewGuid(),
                ProductId = item.ProductId,
                WarehouseId = order.WarehouseId,
                BatchId = item.BatchId,
                MovementType = StockMovementType.SalesOutbound,
                Quantity = -item.Quantity,
                UnitCost = unitCost,
                TotalCost = -totalCost,
                SourceId = order.Id,
                SourceNo = order.ShipmentNo,
                CreatedAt = utcNow,
                CreatedBy = operatorId,
            }, cancellationToken);
        }

        if (linkedOrder is not null)
        {
            foreach (var item in items)
            {
                if (item.OrderItemId is null)
                {
                    continue;
                }

                await _salesOrderRepository.AddFulfilledQuantityAsync(item.OrderItemId.Value, item.Quantity, cancellationToken);
            }

            var flowStatus = DeriveFlowStatus(orderItems, items);
            await _salesOrderRepository.UpdateFlowStatusAsync(linkedOrder.Id, flowStatus, operatorId, cancellationToken);
        }

        // 总账（erp-general-ledger）：同事务生成自动凭证（借应收账款 / 贷主营业务收入，
        // 并附成本结转分录：借主营业务成本 / 贷库存商品）；
        // 科目映射缺失（40158）或期间不可记账（40154 / 40159）会阻断整单，随事务回滚
        await VoucherWriter.AppendAutoAsync(
            VoucherSourceType.SalesOutbound,
            order.Id,
            order.ShipmentNo,
            order.OrderDate,
            order.TotalAmount,
            costAmount,
            null,
            _voucherRepository,
            _accountMappingRepository,
            _accountingPeriodRepository,
            _accountRepository,
            operatorId,
            cancellationToken);
    }

    /// <summary>
    /// 按「本次发货后的累计已发量」推导订单流转状态（erp-order-flow design.md §3.4）：
    /// 全部执行完 → 已完成；部分执行 → 部分发货；否则保持待发货。
    /// </summary>
    private static OrderFlowStatus DeriveFlowStatus(
        IReadOnlyList<SalesOrderItem> orderItems,
        IReadOnlyList<SalesShipmentItem> shippedItems)
    {
        var shipped = new Dictionary<Guid, int>(shippedItems.Count);
        foreach (var item in shippedItems)
        {
            if (item.OrderItemId is null)
            {
                continue;
            }

            var key = item.OrderItemId.Value;
            shipped[key] = shipped.TryGetValue(key, out var accumulated) ? accumulated + item.Quantity : item.Quantity;
        }

        var allFulfilled = orderItems.All(i =>
            i.FulfilledQuantity + (shipped.TryGetValue(i.Id, out var quantity) ? quantity : 0) >= i.Quantity);
        if (allFulfilled)
        {
            return OrderFlowStatus.Completed;
        }

        var anyFulfilled = orderItems.Any(i =>
            i.FulfilledQuantity + (shipped.TryGetValue(i.Id, out var quantity) ? quantity : 0) > 0);
        return anyFulfilled ? OrderFlowStatus.Partial : OrderFlowStatus.Pending;
    }
}
