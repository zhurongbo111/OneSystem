using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Purchases.UpdatePurchaseOrderSettlement;

/// <summary>
/// 采购单结算状态更新用例：不存在 → 40400；已作废 → 40104（防作废后误标已付）；
/// 更新结算状态 + 审计；目标状态与当前相同为无操作（幂等）。库存不变。
/// </summary>
public sealed class UpdatePurchaseOrderSettlementRequestHandler : IRequestHandler<UpdatePurchaseOrderSettlementRequest, PurchaseOrderDetailDto>
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;

    /// <summary>
    /// 初始化采购单结算更新用例处理器
    /// </summary>
    public UpdatePurchaseOrderSettlementRequestHandler(IPurchaseOrderRepository purchaseOrderRepository)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
    }

    /// <summary>
    /// 处理采购单结算更新请求
    /// </summary>
    /// <param name="request">结算更新请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PurchaseOrderDetailDto> HandleAsync(UpdatePurchaseOrderSettlementRequest request, CancellationToken cancellationToken = default)
    {
        var target = (OrderSettlementStatus)request.SettlementStatus;

        var detail = await _purchaseOrderRepository.GetDetailAsync(request.Id, cancellationToken);
        if (detail is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "采购单不存在");
        }

        if (detail.Status == OrderStatus.Voided)
        {
            throw new BusinessException(ErrorCode.OrderVoided, "单据已作废，禁止再操作");
        }

        if (detail.SettlementStatus != target)
        {
            await _purchaseOrderRepository.UpdateSettlementAsync(request.Id, target, cancellationToken);
        }

        var updated = await _purchaseOrderRepository.GetDetailAsync(request.Id, cancellationToken);
        if (updated is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "采购单不存在");
        }

        return PurchaseDtoMapper.ToPurchaseOrderDetailDto(updated);
    }
}
