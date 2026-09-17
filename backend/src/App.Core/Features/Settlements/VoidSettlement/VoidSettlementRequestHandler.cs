using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Settlements.VoidSettlement;

/// <summary>
/// 作废收付款单用例：不存在 → 40400；已作废 → 40104（幂等防重，不重复回退）；
/// 事务内逐条核销明细按被核销单据类型回退已结算金额（−amount）+ 状态置作废 + 审计。
/// 作废后单号不复用；仅改状态，不删数据。
/// </summary>
public sealed class VoidSettlementRequestHandler : IRequestHandler<VoidSettlementRequest, SettlementDetailDto>
{
    private readonly ISettlementRepository _settlementRepository;
    private readonly IPurchaseReceiptRepository _purchaseReceiptRepository;
    private readonly ISalesShipmentRepository _salesShipmentRepository;
    private readonly IPurchaseReturnRepository _purchaseReturnRepository;
    private readonly ISalesReturnRepository _salesReturnRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// 初始化作废收付款单用例处理器
    /// </summary>
    public VoidSettlementRequestHandler(
        ISettlementRepository settlementRepository,
        IPurchaseReceiptRepository purchaseReceiptRepository,
        ISalesShipmentRepository salesShipmentRepository,
        IPurchaseReturnRepository purchaseReturnRepository,
        ISalesReturnRepository salesReturnRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser)
    {
        _settlementRepository = settlementRepository;
        _purchaseReceiptRepository = purchaseReceiptRepository;
        _salesShipmentRepository = salesShipmentRepository;
        _purchaseReturnRepository = purchaseReturnRepository;
        _salesReturnRepository = salesReturnRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    /// <summary>
    /// 处理作废收付款单请求
    /// </summary>
    /// <param name="request">作废请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<SettlementDetailDto> HandleAsync(VoidSettlementRequest request, CancellationToken cancellationToken = default)
    {
        var (settlement, items) = await _settlementRepository.GetDetailAsync(request.Id, cancellationToken);
        if (settlement is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "收付款单不存在");
        }

        if (settlement.Status == OrderStatus.Voided)
        {
            // 已作废禁止再操作（防重复作废 / 重复回退）
            throw new BusinessException(ErrorCode.OrderVoided, "单据已作废，禁止再操作");
        }

        var operatorId = _currentUser.UserId();

        // 回退与状态变更同一事务（design.md §3.4）
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var item in items)
            {
                // 逐条明细按其被核销单据类型回退已结算金额（−amount）
                await AddSettledAmountAsync(item.OrderType, item.OrderId, -item.Amount, operatorId, cancellationToken);
            }

            await _settlementRepository.UpdateStatusAsync(request.Id, OrderStatus.Voided, operatorId, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        var (updated, updatedItems) = await _settlementRepository.GetDetailAsync(request.Id, cancellationToken);
        if (updated is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "收付款单不存在");
        }

        return SettlementsDtoMapper.ToSettlementDetailDto(updated, updatedItems);
    }

    /// <summary>
    /// 按被核销单据类型分派到对应单据仓储，原子累加已结算金额（作废回退传负值）
    /// </summary>
    private Task AddSettledAmountAsync(SettlementOrderType orderType, Guid orderId, decimal delta, Guid? operatorId, CancellationToken cancellationToken)
    {
        if (orderType == SettlementOrderType.PurchaseInbound)
        {
            return _purchaseReceiptRepository.AddSettledAmountAsync(orderId, delta, operatorId, cancellationToken);
        }

        if (orderType == SettlementOrderType.SalesOutbound)
        {
            return _salesShipmentRepository.AddSettledAmountAsync(orderId, delta, operatorId, cancellationToken);
        }

        if (orderType == SettlementOrderType.PurchaseReturn)
        {
            return _purchaseReturnRepository.AddSettledAmountAsync(orderId, delta, operatorId, cancellationToken);
        }

        return _salesReturnRepository.AddSettledAmountAsync(orderId, delta, operatorId, cancellationToken);
    }
}
