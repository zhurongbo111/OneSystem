using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.Settlements.GetSettlements;

/// <summary>
/// 收付款单分页查询用例：仓储分页筛选（含作废单据）→ 映射 DTO（含 Status 供前端置灰）
/// </summary>
public sealed class GetSettlementsRequestHandler : IRequestHandler<GetSettlementsRequest, PagedResult<SettlementListItemDto>>
{
    private readonly ISettlementRepository _settlementRepository;

    /// <summary>
    /// 初始化收付款单分页查询用例处理器
    /// </summary>
    public GetSettlementsRequestHandler(ISettlementRepository settlementRepository)
    {
        _settlementRepository = settlementRepository;
    }

    /// <summary>
    /// 处理收付款单分页查询请求
    /// </summary>
    /// <param name="request">分页查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<SettlementListItemDto>> HandleAsync(GetSettlementsRequest request, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _settlementRepository.GetPagedAsync(
            request.Keyword, request.Type, request.PartnerId, request.Method, request.Start, request.End,
            request.Page, request.PageSize, cancellationToken);

        return new PagedResult<SettlementListItemDto>
        {
            Items = items.Select(SettlementsDtoMapper.ToSettlementListItemDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
