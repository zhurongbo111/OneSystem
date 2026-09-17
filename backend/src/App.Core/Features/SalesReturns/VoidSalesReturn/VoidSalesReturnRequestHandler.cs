using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.SalesReturns.VoidSalesReturn;

/// <summary>
/// 销售退货单作废用例：不存在 → 40400；已作废 → 40104（幂等防重，不重复回冲）；
/// 事务内逐行库存回冲（IncrementAsync(-quantity)，**允许冲负** —— 退回入库的货可能已被再次卖出）
/// + 状态置作废 + 审计。作废后单号不复用；仅改状态，不删数据（明细保留供审计）。
/// </summary>
public sealed class VoidSalesReturnRequestHandler : IRequestHandler<VoidSalesReturnRequest, SalesReturnDetailDto>
{
    private readonly ISalesReturnRepository _salesReturnRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// 初始化销售退货单作废用例处理器
    /// </summary>
    public VoidSalesReturnRequestHandler(
        ISalesReturnRepository salesReturnRepository,
        IInventoryRepository inventoryRepository,
        IStockMovementRepository stockMovementRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser)
    {
        _salesReturnRepository = salesReturnRepository;
        _inventoryRepository = inventoryRepository;
        _stockMovementRepository = stockMovementRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    /// <summary>
    /// 处理销售退货单作废请求
    /// </summary>
    /// <param name="request">作废请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<SalesReturnDetailDto> HandleAsync(VoidSalesReturnRequest request, CancellationToken cancellationToken = default)
    {
        var (salesReturn, items) = await _salesReturnRepository.GetDetailAsync(request.Id, cancellationToken);
        if (salesReturn is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "销售退货单不存在");
        }

        if (salesReturn.Status == OrderStatus.Voided)
        {
            // 已作废禁止再操作（防重复作废 / 重复回冲）
            throw new BusinessException(ErrorCode.OrderVoided, "单据已作废，禁止再操作");
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();

        // 状态流转判定在 Handler（design.md §3.5）：回冲与状态变更同一事务
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // 回冲：逐行库存 -= 退货数量（撤销保存时的回增；允许冲负，见 design.md §5 决策）
            foreach (var item in items)
            {
                await _inventoryRepository.IncrementAsync(item.ProductId, -item.Quantity, cancellationToken);

                // 库存流水：销售退货作废回冲（负方向），与库存增减同事务（design.md §3.7）
                await _stockMovementRepository.AppendAsync(new StockMovement
                {
                    Id = Guid.NewGuid(),
                    ProductId = item.ProductId,
                    MovementType = StockMovementType.SalesReturnVoid,
                    Quantity = -item.Quantity,
                    SourceId = salesReturn.Id,
                    SourceNo = salesReturn.ReturnNo,
                    CreatedAt = now,
                    CreatedBy = operatorId,
                }, cancellationToken);
            }

            await _salesReturnRepository.UpdateStatusAsync(request.Id, OrderStatus.Voided, operatorId, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        var (updatedReturn, updatedItems) = await _salesReturnRepository.GetDetailAsync(request.Id, cancellationToken);
        if (updatedReturn is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "销售退货单不存在");
        }

        return SalesReturnsDtoMapper.ToSalesReturnDetailDto(updatedReturn, updatedItems);
    }
}
