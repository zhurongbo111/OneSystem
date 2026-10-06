using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Finance;

namespace App.Core.Features.SalesReturns;

/// <summary>
/// 销售退货单「生效」组件（specs/042-erp-approval/design.md §3.1）：把「这张单生效」这件事收敛到一处，
/// 由**未命中审批规则的创建路径**（specs/022-erp-sale-return）与**审批通过路径**（specs/042-erp-approval）共用。
/// 生效内容与既有完全一致：按仓 / 按批次库存回增（无上限校验）+ 成本转回 + 库存流水 + 自动凭证（含成本转回分录）。
/// **不自行 Commit**：事务由调用方 <see cref="IUnitOfWork"/> 控制。
/// </summary>
public sealed class SalesReturnFulfillment
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IVoucherRepository _voucherRepository;
    private readonly IAccountMappingRepository _accountMappingRepository;
    private readonly IAccountingPeriodRepository _accountingPeriodRepository;
    private readonly IAccountRepository _accountRepository;

    /// <summary>
    /// 初始化销售退货单生效组件
    /// </summary>
    public SalesReturnFulfillment(
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
    /// 执行销售退货单生效（库存回增 + 成本转回 + 流水 + 自动凭证）
    /// </summary>
    /// <param name="salesReturn">销售退货单主表（已持久化，含仓 / 金额 / 单号快照）</param>
    /// <param name="items">明细行（批次已在创建时解析并落库）</param>
    /// <param name="operatorId">操作人 id（由调用方取 ICurrentUser 传入，可空）</param>
    /// <param name="utcNow">操作时间（由调用方注入，便于单测）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task ApplyAsync(
        SalesReturn salesReturn,
        IReadOnlyList<SalesReturnItem> items,
        Guid? operatorId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        // 库存回增 + 库存流水：销售退货入库无上限校验（erp-sale-return design.md §5），与流水同事务逐行 1:1
        // 成本转回金额 = Σ 本次退货入库成本（自动凭证的成本转回分录金额）
        var costAmount = 0m;
        foreach (var item in items)
        {
            await _inventoryRepository.IncrementAsync(
                item.ProductId, salesReturn.WarehouseId, item.BatchId, item.Quantity, cancellationToken);

            // 成本（erp-cost design §0.2）：按被退销售单原出库成本单价退回；
            // 销售退货不关联原单（specs/021 §5），查不到原流水时兜底按该仓该批次行当前移动加权均价（040 起按批次行）
            var unitCost = await _stockMovementRepository.GetMovementUnitCostAsync(
                salesReturn.Id, item.ProductId, item.BatchId, StockMovementType.SalesOutbound, cancellationToken)
                ?? await _inventoryRepository.GetAverageCostAsync(item.ProductId, salesReturn.WarehouseId, item.BatchId, cancellationToken);
            var totalCost = CostCalculator.TotalCost(item.Quantity, unitCost);
            costAmount += totalCost;
            await _inventoryRepository.ApplyInboundCostAsync(
                item.ProductId, salesReturn.WarehouseId, item.BatchId, item.Quantity, unitCost, cancellationToken);

            await _stockMovementRepository.AppendAsync(new StockMovement
            {
                Id = Guid.NewGuid(),
                ProductId = item.ProductId,
                WarehouseId = salesReturn.WarehouseId,
                BatchId = item.BatchId,
                MovementType = StockMovementType.SalesReturnIn,
                Quantity = item.Quantity,
                UnitCost = unitCost,
                TotalCost = totalCost,
                SourceId = salesReturn.Id,
                SourceNo = salesReturn.ReturnNo,
                CreatedAt = utcNow,
                CreatedBy = operatorId,
            }, cancellationToken);
        }

        // 总账（erp-general-ledger）：同事务生成自动凭证（借主营业务收入 / 贷应收账款，
        // 并附成本转回分录：借库存商品 / 贷主营业务成本）；
        // 科目映射缺失（40158）或期间不可记账（40154 / 40159）会阻断整单，随事务回滚
        await VoucherWriter.AppendAutoAsync(
            VoucherSourceType.SalesReturn,
            salesReturn.Id,
            salesReturn.ReturnNo,
            salesReturn.ReturnDate,
            salesReturn.TotalAmount,
            costAmount,
            null,
            _voucherRepository,
            _accountMappingRepository,
            _accountingPeriodRepository,
            _accountRepository,
            operatorId,
            cancellationToken);
    }
}
