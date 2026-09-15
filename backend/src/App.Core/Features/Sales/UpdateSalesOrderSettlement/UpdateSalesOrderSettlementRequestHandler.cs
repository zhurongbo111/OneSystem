using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Sales;

namespace App.Core.Features.Sales.UpdateSalesOrderSettlement;

/// <summary>
/// 销售单结算状态更新用例：不存在 → 40400；已作废 → 40104（防作废后误标已收）；
/// 更新结算状态 + 审计；目标状态与当前相同为无操作（幂等）。库存不变。
/// </summary>
public sealed class UpdateSalesOrderSettlementRequestHandler : IRequestHandler<UpdateSalesOrderSettlementRequest, SalesOrderDetailDto>
{
    private readonly ISalesOrderRepository _salesOrderRepository;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// 初始化销售单结算更新用例处理器
    /// </summary>
    public UpdateSalesOrderSettlementRequestHandler(
        ISalesOrderRepository salesOrderRepository,
        ICurrentUser currentUser)
    {
        _salesOrderRepository = salesOrderRepository;
        _currentUser = currentUser;
    }

    /// <summary>
    /// 处理销售单结算更新请求
    /// </summary>
    /// <param name="request">结算更新请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<SalesOrderDetailDto> HandleAsync(UpdateSalesOrderSettlementRequest request, CancellationToken cancellationToken = default)
    {
        var target = (OrderSettlementStatus)request.SettlementStatus;

        var detail = await _salesOrderRepository.GetDetailAsync(request.Id, cancellationToken);
        if (detail is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "销售单不存在");
        }

        if (detail.Status == OrderStatus.Voided)
        {
            throw new BusinessException(ErrorCode.OrderVoided, "单据已作废，禁止再操作");
        }

        if (detail.SettlementStatus != target)
        {
            await _salesOrderRepository.UpdateSettlementAsync(request.Id, target, _currentUser.UserId(), cancellationToken);
        }

        var updated = await _salesOrderRepository.GetDetailAsync(request.Id, cancellationToken);
        if (updated is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "销售单不存在");
        }

        return SalesDtoMapper.ToSalesOrderDetailDto(updated);
    }
}
