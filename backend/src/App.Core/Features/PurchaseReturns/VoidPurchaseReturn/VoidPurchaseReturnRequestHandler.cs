using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Finance;

namespace App.Core.Features.PurchaseReturns.VoidPurchaseReturn;

/// <summary>
/// 采购退货单作废用例：不存在 → 40400；已作废 → 40104（幂等防重，不重复回冲）；
/// 已核销（`SettledAmount > 0`）→ 40120（作废与核销互斥，须先作废对应收付款单）；
/// 事务内逐行库存回冲（IncrementAsync(+quantity)，作废必须可执行）+ 状态置作废 + 审计。
/// 作废后单号不复用；仅改状态，不删数据（明细保留供审计）。
/// </summary>
public sealed class VoidPurchaseReturnRequestHandler : IRequestHandler<VoidPurchaseReturnRequest, PurchaseReturnDetailDto>
{
    private readonly IPurchaseReturnRepository _purchaseReturnRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IVoucherRepository _voucherRepository;
    private readonly IAccountingPeriodRepository _accountingPeriodRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化采购退货单作废用例处理器
    /// </summary>
    public VoidPurchaseReturnRequestHandler(
        IPurchaseReturnRepository purchaseReturnRepository,
        IInventoryRepository inventoryRepository,
        IStockMovementRepository stockMovementRepository,
        IVoucherRepository voucherRepository,
        IAccountingPeriodRepository accountingPeriodRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _purchaseReturnRepository = purchaseReturnRepository;
        _inventoryRepository = inventoryRepository;
        _stockMovementRepository = stockMovementRepository;
        _voucherRepository = voucherRepository;
        _accountingPeriodRepository = accountingPeriodRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理采购退货单作废请求
    /// </summary>
    /// <param name="request">作废请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PurchaseReturnDetailDto> HandleAsync(VoidPurchaseReturnRequest request, CancellationToken cancellationToken = default)
    {
        var (purchaseReturn, items) = await _purchaseReturnRepository.GetDetailAsync(request.Id, cancellationToken: cancellationToken);
        if (purchaseReturn is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "采购退货单不存在");
        }

        if (purchaseReturn.Status == OrderStatus.Voided)
        {
            // 已作废禁止再操作（防重复作废 / 重复回冲）
            throw new BusinessException(ErrorCode.OrderVoided, "单据已作废，禁止再操作");
        }

        // 已核销禁止作废：作废与核销互斥（specs/023-erp-settlement design.md §0）——须先作废对应收付款单回退已结金额
        if (purchaseReturn.SettledAmount > 0)
        {
            throw new BusinessException(
                ErrorCode.OrderSettledCannotVoid,
                $"采购退货单 {purchaseReturn.ReturnNo} 已被收付款单核销（已结 {purchaseReturn.SettledAmount:0.00}），请先作废对应收付款单");
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();

        // 状态流转判定在 Handler（design.md §3.4）：回冲与状态变更同一事务
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // 回冲：逐行库存 += 退货数量（把退回供应商的实物收回账上；与保存时的扣减对称）
            foreach (var item in items)
            {
                await _inventoryRepository.IncrementAsync(item.ProductId, item.Quantity, cancellationToken);

                // 成本：冲销还原 —— 复用该退货单原出库流水的成本单价（erp-cost design §0.2）
                var unitCost = await _stockMovementRepository.GetMovementUnitCostAsync(
                    purchaseReturn.Id, item.ProductId, StockMovementType.PurchaseReturnOut, cancellationToken) ?? 0m;
                var totalCost = CostCalculator.TotalCost(item.Quantity, unitCost);
                await _inventoryRepository.ApplyInboundCostAsync(item.ProductId, item.Quantity, unitCost, cancellationToken);

                // 库存流水：采购退货作废回冲（正方向），与库存增减同事务（design.md §3.6）
                await _stockMovementRepository.AppendAsync(new StockMovement
                {
                    Id = Guid.NewGuid(),
                    ProductId = item.ProductId,
                    MovementType = StockMovementType.PurchaseReturnVoid,
                    Quantity = item.Quantity,
                    UnitCost = unitCost,
                    TotalCost = totalCost,
                    SourceId = purchaseReturn.Id,
                    SourceNo = purchaseReturn.ReturnNo,
                    CreatedAt = now,
                    CreatedBy = operatorId,
                }, cancellationToken);
            }

            await _purchaseReturnRepository.UpdateStatusAsync(request.Id, OrderStatus.Voided, operatorId, cancellationToken);

            // 总账（erp-general-ledger）：同事务作废其自动凭证；期间已结账（40154）会阻断作废，随事务回滚
            await VoucherWriter.VoidAutoVouchersAsync(
                VoucherSourceType.PurchaseReturn,
                purchaseReturn.Id,
                purchaseReturn.ReturnDate,
                _voucherRepository,
                _accountingPeriodRepository,
                operatorId,
                cancellationToken);

            // 业务写成功后、提交前追加操作日志：与业务同事务，异常回滚则不产生日志
            var voidedReturnChangeBuilder = new AuditChangeBuilder()
                .Add("status", "单据状态", AuditText.OrderStatus(OrderStatus.Normal), AuditText.OrderStatus(OrderStatus.Voided));
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.PurchaseReturn,
                Action = AuditAction.Void,
                ResourceId = purchaseReturn.Id,
                ResourceNo = purchaseReturn.ReturnNo,
                Summary = $"作废采购退货单 {purchaseReturn.ReturnNo}（供应商：{purchaseReturn.PartnerName}、{AuditSummary.Money(purchaseReturn.TotalAmount)}）",
                Changes = voidedReturnChangeBuilder.Build(),
                ChangesTruncated = voidedReturnChangeBuilder.Truncated,
                UtcNow = now,
            }, cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        var (updatedReturn, updatedItems) = await _purchaseReturnRepository.GetDetailAsync(request.Id, cancellationToken: cancellationToken);
        if (updatedReturn is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "采购退货单不存在");
        }

        return PurchaseReturnsDtoMapper.ToPurchaseReturnDetailDto(updatedReturn, updatedItems);
    }
}
