using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Finance;

namespace App.Core.Features.PurchaseReceipts;

/// <summary>
/// 采购入库单「生效」组件（specs/042-erp-approval/design.md §3.1）：把「这张单生效」这件事收敛到一处，
/// 由**未命中审批规则的创建路径**（specs/015-erp-purchase）与**审批通过路径**（specs/042-erp-approval）共用，
/// 杜绝两套生效逻辑漂移。
/// 生效内容与既有完全一致：按仓 / 按批次库存 += 数量 + 成本加权 + 库存流水 + 关联采购订单累计回写 + 自动凭证。
/// **不自行 Commit**：事务由调用方 <see cref="IUnitOfWork"/> 控制（创建与审批通过都在各自事务内调用）。
/// </summary>
public sealed class PurchaseReceiptFulfillment
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IVoucherRepository _voucherRepository;
    private readonly IAccountMappingRepository _accountMappingRepository;
    private readonly IAccountingPeriodRepository _accountingPeriodRepository;
    private readonly IAccountRepository _accountRepository;

    /// <summary>
    /// 初始化采购入库单生效组件
    /// </summary>
    public PurchaseReceiptFulfillment(
        IInventoryRepository inventoryRepository,
        IStockMovementRepository stockMovementRepository,
        IPurchaseOrderRepository purchaseOrderRepository,
        IVoucherRepository voucherRepository,
        IAccountMappingRepository accountMappingRepository,
        IAccountingPeriodRepository accountingPeriodRepository,
        IAccountRepository accountRepository)
    {
        _inventoryRepository = inventoryRepository;
        _stockMovementRepository = stockMovementRepository;
        _purchaseOrderRepository = purchaseOrderRepository;
        _voucherRepository = voucherRepository;
        _accountMappingRepository = accountMappingRepository;
        _accountingPeriodRepository = accountingPeriodRepository;
        _accountRepository = accountRepository;
    }

    /// <summary>
    /// 执行采购入库单生效（库存回增 + 成本 + 流水 + 订单累计回写 + 自动凭证）
    /// </summary>
    /// <param name="order">采购入库单主表（已持久化，含仓 / 金额 / 单号快照）</param>
    /// <param name="items">明细行（批次已在创建时解析并落库）</param>
    /// <param name="operatorId">操作人 id（由调用方取 ICurrentUser 传入，可空）</param>
    /// <param name="utcNow">操作时间（由调用方注入，便于单测）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task ApplyAsync(
        PurchaseReceipt order,
        IReadOnlyList<PurchaseReceiptItem> items,
        Guid? operatorId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        // 关联订单：先取当前状态与明细（用于按「本次收货后的累计已收量」推导订单流转状态）
        PurchaseOrder? linkedOrder = null;
        IReadOnlyList<PurchaseOrderItem> orderItems = Array.Empty<PurchaseOrderItem>();
        if (order.OrderId is not null)
        {
            var (found, lines) = await _purchaseOrderRepository.GetDetailAsync(order.OrderId.Value, cancellationToken);
            linkedOrder = found;
            orderItems = lines;
        }

        foreach (var item in items)
        {
            // 采购入库：入库仓库存 += 数量（040 起按批次行；同事务，回冲在作废用例执行）
            await _inventoryRepository.IncrementAsync(
                item.ProductId, order.WarehouseId, item.BatchId, item.Quantity, cancellationToken);

            // 成本：入库按采购单明细单价加权（erp-cost design §0.2；040 起按批次行记账）—— 先加数量再加金额
            await _inventoryRepository.ApplyInboundCostAsync(
                item.ProductId, order.WarehouseId, item.BatchId, item.Quantity, item.UnitPrice, cancellationToken);

            // 库存流水：与库存增减同事务，1:1 追加并带变动仓（erp-stock-movement design §3.7；040 带批次）
            await _stockMovementRepository.AppendAsync(new StockMovement
            {
                Id = Guid.NewGuid(),
                ProductId = item.ProductId,
                WarehouseId = order.WarehouseId,
                BatchId = item.BatchId,
                MovementType = StockMovementType.PurchaseInbound,
                Quantity = item.Quantity,
                UnitCost = item.UnitPrice,
                TotalCost = CostCalculator.TotalCost(item.Quantity, item.UnitPrice),
                SourceId = order.Id,
                SourceNo = order.ReceiptNo,
                CreatedAt = utcNow,
                CreatedBy = operatorId,
            }, cancellationToken);
        }

        if (linkedOrder is not null)
        {
            // 回写订单明细累计已收（原子累加），再按「本次累计后的未执行量」推导订单状态
            foreach (var item in items)
            {
                if (item.OrderItemId is null)
                {
                    continue;
                }

                await _purchaseOrderRepository.AddFulfilledQuantityAsync(item.OrderItemId.Value, item.Quantity, cancellationToken);
            }

            var flowStatus = DeriveFlowStatus(orderItems, items);
            await _purchaseOrderRepository.UpdateFlowStatusAsync(linkedOrder.Id, flowStatus, operatorId, cancellationToken);
        }

        // 总账（erp-general-ledger）：同事务生成自动凭证（借存货 / 贷应付账款）；
        // 科目映射缺失（40158）或期间不可记账（40154 / 40159）会阻断整单，随事务回滚
        await VoucherWriter.AppendAutoAsync(
            VoucherSourceType.PurchaseInbound,
            order.Id,
            order.ReceiptNo,
            order.OrderDate,
            order.TotalAmount,
            0m,
            null,
            _voucherRepository,
            _accountMappingRepository,
            _accountingPeriodRepository,
            _accountRepository,
            operatorId,
            cancellationToken);
    }

    /// <summary>
    /// 按「本次收货后的累计已收量」推导订单流转状态（erp-order-flow design.md §3.4）：
    /// 全部执行完 → 已完成；部分执行 → 部分收货；否则保持待收货。
    /// </summary>
    private static OrderFlowStatus DeriveFlowStatus(
        IReadOnlyList<PurchaseOrderItem> orderItems,
        IReadOnlyList<PurchaseReceiptItem> receivedItems)
    {
        var received = new Dictionary<Guid, int>(receivedItems.Count);
        foreach (var item in receivedItems)
        {
            if (item.OrderItemId is null)
            {
                continue;
            }

            var key = item.OrderItemId.Value;
            received[key] = received.TryGetValue(key, out var accumulated) ? accumulated + item.Quantity : item.Quantity;
        }

        var allFulfilled = orderItems.All(i =>
            i.FulfilledQuantity + (received.TryGetValue(i.Id, out var quantity) ? quantity : 0) >= i.Quantity);
        if (allFulfilled)
        {
            return OrderFlowStatus.Completed;
        }

        var anyFulfilled = orderItems.Any(i =>
            i.FulfilledQuantity + (received.TryGetValue(i.Id, out var quantity) ? quantity : 0) > 0);
        return anyFulfilled ? OrderFlowStatus.Partial : OrderFlowStatus.Pending;
    }
}
