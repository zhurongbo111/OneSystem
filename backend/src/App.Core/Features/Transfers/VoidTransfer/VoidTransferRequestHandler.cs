using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Transfers.VoidTransfer;

/// <summary>
/// 调拨单作废用例：不存在 → 40400；已作废 → 40104（幂等防重，不重复回冲）；
/// 事务内逐行双向回冲（转入仓 −q、转出仓 +q，允许冲负）+ 两条反向流水（TransferInVoid / TransferOutVoid）+ 状态置作废 + 审计。
/// 原单价取法：原转入流水 UnitCost（026 §3.1 GetMovementUnitCostAsync），取不到按当前均价兜底。
/// 作废后单号不复用；仅改状态，不删数据（明细保留供审计）。
/// </summary>
public sealed class VoidTransferRequestHandler : IRequestHandler<VoidTransferRequest, TransferDetailDto>
{
    private readonly ITransferRepository _transferRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化调拨单作废用例处理器
    /// </summary>
    public VoidTransferRequestHandler(
        ITransferRepository transferRepository,
        IInventoryRepository inventoryRepository,
        IStockMovementRepository stockMovementRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _transferRepository = transferRepository;
        _inventoryRepository = inventoryRepository;
        _stockMovementRepository = stockMovementRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理调拨单作废请求
    /// </summary>
    /// <param name="request">作废请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<TransferDetailDto> HandleAsync(VoidTransferRequest request, CancellationToken cancellationToken = default)
    {
        var (transfer, items) = await _transferRepository.GetDetailAsync(request.Id, cancellationToken: cancellationToken);
        if (transfer is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "调拨单不存在");
        }

        if (transfer.Status == OrderStatus.Voided)
        {
            // 已作废禁止再操作（防重复作废 / 重复回冲）
            throw new BusinessException(ErrorCode.OrderVoided, "单据已作废，禁止再操作");
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();

        // 状态流转判定在 Handler（design.md §3.4）：回冲与状态变更同一事务
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var item in items)
            {
                // 原转入流水单价（026 §3.1），取不到（异常数据）按转入仓当前均价兜底
                var unitCost = await _stockMovementRepository.GetMovementUnitCostAsync(
                    transfer.Id, item.ProductId, StockMovementType.TransferIn, cancellationToken)
                    ?? await _inventoryRepository.GetAverageCostAsync(item.ProductId, transfer.ToWarehouseId, cancellationToken);
                var totalCost = CostCalculator.TotalCost(item.Quantity, unitCost);

                // 转入仓回冲：IncrementAsync(-q) + ApplyOutboundCost + AppendAsync(TransferInVoid, -q)
                await _inventoryRepository.IncrementAsync(
                    item.ProductId, transfer.ToWarehouseId, -item.Quantity, cancellationToken);
                await _inventoryRepository.ApplyOutboundCostAsync(
                    item.ProductId, transfer.ToWarehouseId, totalCost, cancellationToken);
                await _stockMovementRepository.AppendAsync(new StockMovement
                {
                    Id = Guid.NewGuid(),
                    ProductId = item.ProductId,
                    WarehouseId = transfer.ToWarehouseId,
                    MovementType = StockMovementType.TransferInVoid,
                    Quantity = -item.Quantity,
                    UnitCost = unitCost,
                    TotalCost = -totalCost,
                    SourceId = transfer.Id,
                    SourceNo = transfer.TransferNo,
                    CreatedAt = now,
                    CreatedBy = operatorId,
                }, cancellationToken);

                // 转出仓回冲：IncrementAsync(+q) + ApplyInboundCost + AppendAsync(TransferOutVoid, +q)
                await _inventoryRepository.IncrementAsync(
                    item.ProductId, transfer.FromWarehouseId, item.Quantity, cancellationToken);
                await _inventoryRepository.ApplyInboundCostAsync(
                    item.ProductId, transfer.FromWarehouseId, item.Quantity, unitCost, cancellationToken);
                await _stockMovementRepository.AppendAsync(new StockMovement
                {
                    Id = Guid.NewGuid(),
                    ProductId = item.ProductId,
                    WarehouseId = transfer.FromWarehouseId,
                    MovementType = StockMovementType.TransferOutVoid,
                    Quantity = item.Quantity,
                    UnitCost = unitCost,
                    TotalCost = totalCost,
                    SourceId = transfer.Id,
                    SourceNo = transfer.TransferNo,
                    CreatedAt = now,
                    CreatedBy = operatorId,
                }, cancellationToken);
            }

            await _transferRepository.UpdateStatusAsync(request.Id, OrderStatus.Voided, operatorId, cancellationToken);

            // 业务写成功后、提交前追加操作日志：与业务同事务，异常回滚则不产生日志
            var voidedTransferChangeBuilder = new AuditChangeBuilder()
                .Add("status", "单据状态", AuditText.OrderStatus(OrderStatus.Normal), AuditText.OrderStatus(OrderStatus.Voided));
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Transfer,
                Action = AuditAction.Void,
                ResourceId = transfer.Id,
                ResourceNo = transfer.TransferNo,
                Summary = $"作废调拨单 {transfer.TransferNo}（转出仓：{transfer.FromWarehouseName}、转入仓：{transfer.ToWarehouseName}、{AuditSummary.Quantity(transfer.TotalQuantity)}）",
                Changes = voidedTransferChangeBuilder.Build(),
                ChangesTruncated = voidedTransferChangeBuilder.Truncated,
                UtcNow = now,
            }, cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        var (updatedTransfer, updatedItems) = await _transferRepository.GetDetailAsync(request.Id, cancellationToken: cancellationToken);
        if (updatedTransfer is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "调拨单不存在");
        }

        return TransfersDtoMapper.ToTransferDetailDto(updatedTransfer, updatedItems);
    }
}
