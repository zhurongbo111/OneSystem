using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.PurchaseOrders.GetPurchaseOrderById;

/// <summary>
/// 采购订单详情查询用例：不存在 → 40400；明细含累计已收 / 未收数量，快照字段原样返回
/// </summary>
public sealed class GetPurchaseOrderByIdRequestHandler : IRequestHandler<GetPurchaseOrderByIdRequest, PurchaseOrderDetailDto>
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;

    /// <summary>
    /// 初始化采购订单详情查询用例处理器
    /// </summary>
    public GetPurchaseOrderByIdRequestHandler(IPurchaseOrderRepository purchaseOrderRepository)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
    }

    /// <summary>
    /// 处理采购订单详情查询请求
    /// </summary>
    /// <param name="request">详情查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PurchaseOrderDetailDto> HandleAsync(GetPurchaseOrderByIdRequest request, CancellationToken cancellationToken = default)
    {
        var (order, items) = await _purchaseOrderRepository.GetDetailAsync(request.Id, cancellationToken);
        if (order is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "采购订单不存在");
        }

        return PurchaseOrdersDtoMapper.ToPurchaseOrderDetailDto(order, items);
    }
}
