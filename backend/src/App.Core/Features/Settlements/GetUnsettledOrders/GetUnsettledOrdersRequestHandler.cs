using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.Settlements.GetUnsettledOrders;

/// <summary>
/// 可核销单据候选查询用例：按方向推导可核销单据类型集合后，委托跨表只读仓储返回未结单据
/// （收款 → 销售出库单 + 采购退货单；付款 → 采购入库单 + 销售退货单）
/// </summary>
public sealed class GetUnsettledOrdersRequestHandler : IRequestHandler<GetUnsettledOrdersRequest, PagedResult<SettlementCandidateDto>>
{
    private readonly ISettlementQueryRepository _settlementQueryRepository;

    /// <summary>
    /// 初始化可核销单据候选查询用例处理器
    /// </summary>
    public GetUnsettledOrdersRequestHandler(ISettlementQueryRepository settlementQueryRepository)
    {
        _settlementQueryRepository = settlementQueryRepository;
    }

    /// <summary>
    /// 处理可核销单据候选查询请求
    /// </summary>
    /// <param name="request">查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<SettlementCandidateDto>> HandleAsync(GetUnsettledOrdersRequest request, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _settlementQueryRepository.GetUnsettledAsync(
            request.PartnerId, request.Type, request.Page, request.PageSize, cancellationToken);

        return new PagedResult<SettlementCandidateDto>
        {
            Items = items.Select(SettlementsDtoMapper.ToSettlementCandidateDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
