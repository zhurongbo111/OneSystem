using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Purchases.VoidPurchaseOrder;

/// <summary>
/// 采购单作废用例：不存在 → 40400；已作废 → 40104（幂等防重，不重复回冲）；
/// 事务内逐行库存回冲（IncrementAsync(-quantity)，允许冲负）+ 状态置作废 + 审计。
/// 作废后单号不复用；仅改状态，不删数据。
/// </summary>
public sealed class VoidPurchaseOrderRequestHandler : IRequestHandler<VoidPurchaseOrderRequest, PurchaseOrderDetailDto>
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// 初始化采购单作废用例处理器
    /// </summary>
    public VoidPurchaseOrderRequestHandler(
        IPurchaseOrderRepository purchaseOrderRepository,
        IInventoryRepository inventoryRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
        _inventoryRepository = inventoryRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    /// <summary>
    /// 处理采购单作废请求
    /// </summary>
    /// <param name="request">作废请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PurchaseOrderDetailDto> HandleAsync(VoidPurchaseOrderRequest request, CancellationToken cancellationToken = default)
    {
        var (order, items) = await _purchaseOrderRepository.GetDetailAsync(request.Id, cancellationToken);
        if (order is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "采购单不存在");
        }

        if (order.Status == OrderStatus.Voided)
        {
            // 已作废禁止再操作（防重复作废 / 重复回冲）
            throw new BusinessException(ErrorCode.OrderVoided, "单据已作废，禁止再操作");
        }

        // 状态流转判定在 Handler（design.md §3.4）：回冲与状态变更同一事务
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // 回冲：逐行库存 -= 数量（与入库同一事务；允许冲负，见 design.md §5 决策）
            foreach (var item in items)
            {
                await _inventoryRepository.IncrementAsync(item.ProductId, -item.Quantity, cancellationToken);
            }

            await _purchaseOrderRepository.UpdateStatusAsync(request.Id, OrderStatus.Voided, _currentUser.UserId(), cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        var (updatedOrder, updatedItems) = await _purchaseOrderRepository.GetDetailAsync(request.Id, cancellationToken);
        if (updatedOrder is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "采购单不存在");
        }

        return PurchaseDtoMapper.ToPurchaseOrderDetailDto(updatedOrder, updatedItems);
    }
}
