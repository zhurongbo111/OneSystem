using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.PurchaseReceipts.GetPurchaseReceiptById;

/// <summary>
/// 采购单详情查询用例：不存在 → 40400；快照字段原样返回
/// </summary>
public sealed class GetPurchaseReceiptByIdRequestHandler : IRequestHandler<GetPurchaseReceiptByIdRequest, PurchaseReceiptDetailDto>
{
    private readonly IPurchaseReceiptRepository _purchaseReceiptRepository;

    /// <summary>
    /// 初始化采购单详情查询用例处理器
    /// </summary>
    public GetPurchaseReceiptByIdRequestHandler(IPurchaseReceiptRepository purchaseReceiptRepository)
    {
        _purchaseReceiptRepository = purchaseReceiptRepository;
    }

    /// <summary>
    /// 处理采购单详情查询请求
    /// </summary>
    /// <param name="request">详情查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PurchaseReceiptDetailDto> HandleAsync(GetPurchaseReceiptByIdRequest request, CancellationToken cancellationToken = default)
    {
        var (order, items) = await _purchaseReceiptRepository.GetDetailAsync(request.Id, cancellationToken);
        if (order is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "采购单不存在");
        }

        return PurchaseReceiptsDtoMapper.ToPurchaseReceiptDetailDto(order, items);
    }
}
