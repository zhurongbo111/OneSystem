using App.Core.Abstractions;
using App.Core.Features.Purchases;
using App.Core.Errors;

namespace App.Core.Features.Purchases.GetPurchaseOrderById;

/// <summary>
/// 采购单详情查询用例：不存在 → 40400；快照字段原样返回
/// </summary>
public sealed class GetPurchaseOrderByIdRequestHandler : IRequestHandler<GetPurchaseOrderByIdRequest, PurchaseOrderDetailDto>
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;

    /// <summary>
    /// 初始化采购单详情查询用例处理器
    /// </summary>
    public GetPurchaseOrderByIdRequestHandler(IPurchaseOrderRepository purchaseOrderRepository)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
    }

    /// <summary>
    /// 处理采购单详情查询请求
    /// </summary>
    /// <param name="request">详情查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PurchaseOrderDetailDto> HandleAsync(GetPurchaseOrderByIdRequest request, CancellationToken cancellationToken = default)
    {
        var detail = await _purchaseOrderRepository.GetDetailAsync(request.Id, cancellationToken);
        if (detail is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "采购单不存在");
        }

        return PurchaseDtoMapper.ToPurchaseOrderDetailDto(detail);
    }
}
