using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Finance;

namespace App.Core.Features.PurchaseReturns;

/// <summary>
/// 采购退货单「生效」组件（specs/042-erp-approval/design.md §3.1）：把「这张单生效」这件事收敛到一处，
/// 由**未命中审批规则的创建路径**（specs/021-erp-purchase-return）与**审批通过路径**（specs/042-erp-approval）共用。
/// 生效内容与既有完全一致：按仓 / 按批次条件扣减库存（不足 → 40103）+ 成本结转 + 库存流水 + 自动凭证。
/// **不自行 Commit**：事务由调用方 <see cref="IUnitOfWork"/> 控制。
/// </summary>
public sealed class PurchaseReturnFulfillment
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IVoucherRepository _voucherRepository;
    private readonly IAccountMappingRepository _accountMappingRepository;
    private readonly IAccountingPeriodRepository _accountingPeriodRepository;
    private readonly IAccountRepository _accountRepository;

    /// <summary>
    /// 初始化采购退货单生效组件
    /// </summary>
    public PurchaseReturnFulfillment(
        IInventoryRepository inventoryRepository,
        IStockMovementRepository stockMovementRepository,
        IVoucherRepository voucherRepository,
        IAccountMappingRepository accountMappingRepository,
        IAccountingPeriodRepository accountingPeriodRepository,
        IAccountRepository accountRepository)
    {
        _inventoryRepository = inventoryRepository;
        _stockMovementRepository = stockMovementRepository;
        _voucherRepository = voucherRepository;
        _accountMappingRepository = accountMappingRepository;
        _accountingPeriodRepository = accountingPeriodRepository;
        _accountRepository = accountRepository;
    }

    /// <summary>
    /// 执行采购退货单生效（条件扣减库存 + 成本结转 + 流水 + 自动凭证）；
    /// 任一行库存不足 → 抛 40103，由调用方回滚整单（审批通过时保持「待审批」可重试）
    /// </summary>
    /// <param name="purchaseReturn">采购退货单主表（已持久化，含仓 / 金额 / 单号快照）</param>
    /// <param name="items">明细行（批次已在创建时解析并落库）</param>
    /// <param name="operatorId">操作人 id（由调用方取 ICurrentUser 传入，可空）</param>
    /// <param name="utcNow">操作时间（由调用方注入，便于单测）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task ApplyAsync(
        PurchaseReturn purchaseReturn,
        IReadOnlyList<PurchaseReturnItem> items,
        Guid? operatorId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        // 成本：退货出库按「变动前」该仓该批次行移动加权均价结转（erp-cost design §0.2；040 起按批次行）—— 必须在扣减前读取
        var returnUnitCosts = new List<decimal>(items.Count);
        foreach (var item in items)
        {
            returnUnitCosts.Add(
                await _inventoryRepository.GetAverageCostAsync(item.ProductId, purchaseReturn.WarehouseId, item.BatchId, cancellationToken));
        }

        // 逐行按仓按批次扣减：任一行该批次行库存不足 → 抛 40103（message 含仓名 / 批次号）
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var ok = await _inventoryRepository.TryDecrementAsync(
                item.ProductId, purchaseReturn.WarehouseId, item.BatchId, item.Quantity, cancellationToken);
            if (!ok)
            {
                var current = await _inventoryRepository.GetQuantityAsync(
                    item.ProductId, purchaseReturn.WarehouseId, item.BatchId, cancellationToken);
                throw new BusinessException(
                    ErrorCode.InsufficientStock,
                    $"库存不足：{purchaseReturn.WarehouseName} 商品 {item.ProductName}"
                    + (item.BatchNo is not null ? $" 批次 {item.BatchNo}" : string.Empty)
                    + $"（当前 {current}，需要 {item.Quantity}）");
            }
        }

        // 库存流水：采购退货出库（负方向），与库存扣减同事务
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];

            // 成本：退货出库按变动前批次行均价结转（erp-cost design §0.2；040 起按批次行）
            var unitCost = returnUnitCosts[index];
            var totalCost = CostCalculator.TotalCost(item.Quantity, unitCost);
            await _inventoryRepository.ApplyOutboundCostAsync(
                item.ProductId, purchaseReturn.WarehouseId, item.BatchId, totalCost, cancellationToken);

            await _stockMovementRepository.AppendAsync(new StockMovement
            {
                Id = Guid.NewGuid(),
                ProductId = item.ProductId,
                WarehouseId = purchaseReturn.WarehouseId,
                BatchId = item.BatchId,
                MovementType = StockMovementType.PurchaseReturnOut,
                Quantity = -item.Quantity,
                UnitCost = unitCost,
                TotalCost = -totalCost,
                SourceId = purchaseReturn.Id,
                SourceNo = purchaseReturn.ReturnNo,
                CreatedAt = utcNow,
                CreatedBy = operatorId,
            }, cancellationToken);
        }

        // 总账（erp-general-ledger）：同事务生成自动凭证（借应付账款 / 贷存货）；
        // 科目映射缺失（40158）或期间不可记账（40154 / 40159）会阻断整单，随事务回滚
        await VoucherWriter.AppendAutoAsync(
            VoucherSourceType.PurchaseReturn,
            purchaseReturn.Id,
            purchaseReturn.ReturnNo,
            purchaseReturn.ReturnDate,
            purchaseReturn.TotalAmount,
            0m,
            null,
            _voucherRepository,
            _accountMappingRepository,
            _accountingPeriodRepository,
            _accountRepository,
            operatorId,
            cancellationToken);
    }
}
