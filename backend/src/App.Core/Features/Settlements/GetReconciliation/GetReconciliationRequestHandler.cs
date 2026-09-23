using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.Settlements.GetReconciliation;

/// <summary>
/// 往来对账台账查询用例：委托跨表只读仓储按往来单位聚合应收 / 应付余额与未结单据数 → 映射 DTO
/// </summary>
public sealed class GetReconciliationRequestHandler : IRequestHandler<GetReconciliationRequest, PagedResult<ReconciliationListItemDto>>
{
    private readonly ISettlementQueryRepository _settlementQueryRepository;

    /// <summary>
    /// 初始化往来对账台账查询用例处理器
    /// </summary>
    public GetReconciliationRequestHandler(ISettlementQueryRepository settlementQueryRepository)
    {
        _settlementQueryRepository = settlementQueryRepository;
    }

    /// <summary>
    /// 处理往来对账台账查询请求
    /// </summary>
    /// <param name="request">查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<ReconciliationListItemDto>> HandleAsync(GetReconciliationRequest request, CancellationToken cancellationToken = default)
    {
        // 逾期判定基准日由用例给定（仓储不读系统时间）；「今天」按 UTC 取业务日期同一坐标系
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        var (items, total) = await _settlementQueryRepository.GetReconciliationAsync(
            request.Keyword, request.Type, request.OverdueOnly, today, request.Page, request.PageSize, cancellationToken);

        return new PagedResult<ReconciliationListItemDto>
        {
            Items = items.Select(SettlementsDtoMapper.ToReconciliationListItemDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}