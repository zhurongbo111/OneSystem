using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Sales.VoidSalesOrder;

/// <summary>
/// 销售单作废用例：不存在 → 40400；已作废 → 40104（幂等防重，不重复回冲）；
/// 事务内逐行库存回冲（IncrementAsync(+quantity)，直接加回）+ 状态置作废 + 审计。
/// 作废后单号不复用；仅改状态，不删数据。
/// </summary>
public sealed class VoidSalesOrderRequestHandler : IRequestHandler<VoidSalesOrderRequest, SalesOrderDetailDto>
{
    private readonly ISalesOrderRepository _salesOrderRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// 初始化销售单作废用例处理器
    /// </summary>
    public VoidSalesOrderRequestHandler(
        ISalesOrderRepository salesOrderRepository,
        IInventoryRepository inventoryRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser)
    {
        _salesOrderRepository = salesOrderRepository;
        _inventoryRepository = inventoryRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    /// <summary>
    /// 处理销售单作废请求
    /// </summary>
    /// <param name="request">作废请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<SalesOrderDetailDto> HandleAsync(VoidSalesOrderRequest request, CancellationToken cancellationToken = default)
    {
        var (order, items) = await _salesOrderRepository.GetDetailAsync(request.Id, cancellationToken);
        if (order is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "销售单不存在");
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
            // 回冲：逐行库存 += 数量（与销售扣减同事务；直接加回，无前置校验，见 design.md §5 决策）
            foreach (var item in items)
            {
                await _inventoryRepository.IncrementAsync(item.ProductId, item.Quantity, cancellationToken);
            }

            await _salesOrderRepository.UpdateStatusAsync(request.Id, OrderStatus.Voided, _currentUser.UserId(), cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        var (updatedOrder, updatedItems) = await _salesOrderRepository.GetDetailAsync(request.Id, cancellationToken);
        if (updatedOrder is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "销售单不存在");
        }

        return SalesDtoMapper.ToSalesOrderDetailDto(updatedOrder, updatedItems);
    }
}
