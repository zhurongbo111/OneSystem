using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.PurchaseReturns.UpdatePurchaseReturnSettlement;

/// <summary>
/// 采购退货单结算状态更新用例：不存在 → 40400；已作废 → 40104（防作废后误标已结算）；
/// 更新结算状态 + 审计；目标状态与当前相同为无操作（幂等）。库存不变。
/// </summary>
public sealed class UpdatePurchaseReturnSettlementRequestHandler : IRequestHandler<UpdatePurchaseReturnSettlementRequest, PurchaseReturnDetailDto>
{
    private readonly IPurchaseReturnRepository _purchaseReturnRepository;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// 初始化采购退货单结算更新用例处理器
    /// </summary>
    public UpdatePurchaseReturnSettlementRequestHandler(
        IPurchaseReturnRepository purchaseReturnRepository,
        ICurrentUser currentUser)
    {
        _purchaseReturnRepository = purchaseReturnRepository;
        _currentUser = currentUser;
    }

    /// <summary>
    /// 处理采购退货单结算更新请求
    /// </summary>
    /// <param name="request">结算更新请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PurchaseReturnDetailDto> HandleAsync(UpdatePurchaseReturnSettlementRequest request, CancellationToken cancellationToken = default)
    {
        var target = (OrderSettlementStatus)request.SettlementStatus;

        var (purchaseReturn, _) = await _purchaseReturnRepository.GetDetailAsync(request.Id, cancellationToken);
        if (purchaseReturn is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "采购退货单不存在");
        }

        if (purchaseReturn.Status == OrderStatus.Voided)
        {
            throw new BusinessException(ErrorCode.OrderVoided, "单据已作废，禁止再操作");
        }

        if (purchaseReturn.SettlementStatus != target)
        {
            await _purchaseReturnRepository.UpdateSettlementAsync(request.Id, target, _currentUser.UserId(), cancellationToken);
        }

        var (updatedReturn, updatedItems) = await _purchaseReturnRepository.GetDetailAsync(request.Id, cancellationToken);
        if (updatedReturn is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "采购退货单不存在");
        }

        return PurchaseReturnsDtoMapper.ToPurchaseReturnDetailDto(updatedReturn, updatedItems);
    }
}
