using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.SalesReturns.UpdateSalesReturnSettlement;

/// <summary>
/// 销售退货单结算状态更新用例：不存在 → 40400；已作废 → 40104（防作废后误标已结算）；
/// 更新结算状态 + 审计；目标状态与当前相同为无操作（幂等）。库存不变。
/// </summary>
public sealed class UpdateSalesReturnSettlementRequestHandler : IRequestHandler<UpdateSalesReturnSettlementRequest, SalesReturnDetailDto>
{
    private readonly ISalesReturnRepository _salesReturnRepository;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// 初始化销售退货单结算更新用例处理器
    /// </summary>
    public UpdateSalesReturnSettlementRequestHandler(
        ISalesReturnRepository salesReturnRepository,
        ICurrentUser currentUser)
    {
        _salesReturnRepository = salesReturnRepository;
        _currentUser = currentUser;
    }

    /// <summary>
    /// 处理销售退货单结算更新请求
    /// </summary>
    /// <param name="request">结算更新请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<SalesReturnDetailDto> HandleAsync(UpdateSalesReturnSettlementRequest request, CancellationToken cancellationToken = default)
    {
        var target = (OrderSettlementStatus)request.SettlementStatus;

        var (salesReturn, _) = await _salesReturnRepository.GetDetailAsync(request.Id, cancellationToken);
        if (salesReturn is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "销售退货单不存在");
        }

        if (salesReturn.Status == OrderStatus.Voided)
        {
            throw new BusinessException(ErrorCode.OrderVoided, "单据已作废，禁止再操作");
        }

        if (salesReturn.SettlementStatus != target)
        {
            await _salesReturnRepository.UpdateSettlementAsync(request.Id, target, _currentUser.UserId(), cancellationToken);
        }

        var (updatedReturn, updatedItems) = await _salesReturnRepository.GetDetailAsync(request.Id, cancellationToken);
        if (updatedReturn is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "销售退货单不存在");
        }

        return SalesReturnsDtoMapper.ToSalesReturnDetailDto(updatedReturn, updatedItems);
    }
}
