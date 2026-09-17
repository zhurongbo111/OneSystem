using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.Settlements.GetSettlementById;

/// <summary>
/// 收付款单详情查询用例：不存在 → 40400；核销明细快照原样返回
/// </summary>
public sealed class GetSettlementByIdRequestHandler : IRequestHandler<GetSettlementByIdRequest, SettlementDetailDto>
{
    private readonly ISettlementRepository _settlementRepository;

    /// <summary>
    /// 初始化收付款单详情查询用例处理器
    /// </summary>
    public GetSettlementByIdRequestHandler(ISettlementRepository settlementRepository)
    {
        _settlementRepository = settlementRepository;
    }

    /// <summary>
    /// 处理收付款单详情查询请求
    /// </summary>
    /// <param name="request">详情查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<SettlementDetailDto> HandleAsync(GetSettlementByIdRequest request, CancellationToken cancellationToken = default)
    {
        var (settlement, items) = await _settlementRepository.GetDetailAsync(request.Id, cancellationToken);
        if (settlement is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "收付款单不存在");
        }

        return SettlementsDtoMapper.ToSettlementDetailDto(settlement, items);
    }
}
