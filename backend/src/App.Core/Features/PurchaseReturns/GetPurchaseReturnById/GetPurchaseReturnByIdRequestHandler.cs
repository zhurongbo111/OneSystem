using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.PurchaseReturns.GetPurchaseReturnById;

/// <summary>
/// 采购退货单详情查询用例：不存在 → 40400；快照字段原样返回
/// </summary>
public sealed class GetPurchaseReturnByIdRequestHandler : IRequestHandler<GetPurchaseReturnByIdRequest, PurchaseReturnDetailDto>
{
    private readonly IPurchaseReturnRepository _purchaseReturnRepository;

    /// <summary>
    /// 初始化采购退货单详情查询用例处理器
    /// </summary>
    public GetPurchaseReturnByIdRequestHandler(IPurchaseReturnRepository purchaseReturnRepository)
    {
        _purchaseReturnRepository = purchaseReturnRepository;
    }

    /// <summary>
    /// 处理采购退货单详情查询请求
    /// </summary>
    /// <param name="request">详情查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PurchaseReturnDetailDto> HandleAsync(GetPurchaseReturnByIdRequest request, CancellationToken cancellationToken = default)
    {
        var (purchaseReturn, items) = await _purchaseReturnRepository.GetDetailAsync(request.Id, cancellationToken: cancellationToken);
        if (purchaseReturn is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "采购退货单不存在");
        }

        return PurchaseReturnsDtoMapper.ToPurchaseReturnDetailDto(purchaseReturn, items);
    }
}
