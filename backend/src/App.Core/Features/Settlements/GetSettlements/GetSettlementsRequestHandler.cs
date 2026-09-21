using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Responses;

namespace App.Core.Features.Settlements.GetSettlements;

/// <summary>
/// 收付款单分页查询用例：仓储分页筛选（含作废单据）→ 映射 DTO（含 Status 供前端置灰）。
/// 本页核销明细一次批量取回：聚合「单据类型」集合（OrderTypes，由明细派生不落列，供列表展示）；
/// 传入 orderType + orderId 时额外附带该单据在每张收付款单上的本次核销金额。
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
            request.OrderType, request.OrderId, request.Page, request.PageSize, cancellationToken);

        // 本页核销明细一次批量取回（避免逐单查询 N+1）：既供「单据类型」列聚合，也供按单据反查的核销金额
        IReadOnlyList<SettlementItem> detailItems = items.Count == 0
            ? Array.Empty<SettlementItem>()
            : await _settlementRepository.GetItemsBySettlementIdsAsync(items.Select(s => s.Id).ToList(), cancellationToken);

        var orderTypesBySettlement = detailItems
            .GroupBy(i => i.SettlementId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<int>)g.Select(i => (int)i.OrderType).Distinct().OrderBy(t => t).ToList());
        var amountBySettlement = GetOrderAmounts(request, detailItems);

        return new PagedResult<SettlementListItemDto>
        {
            Items = items
                .Select(s => SettlementsDtoMapper.ToSettlementListItemDto(
                    s,
                    amountBySettlement.TryGetValue(s.Id, out var amount) ? amount : null,
                    orderTypesBySettlement.TryGetValue(s.Id, out var orderTypes) ? orderTypes : Array.Empty<int>()))
                .ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }

    /// <summary>
    /// 汇总每张收付款单对被核销单据的核销金额（未指定被核销单据时返回空表）。
    /// </summary>
    private static Dictionary<Guid, decimal> GetOrderAmounts(
        GetSettlementsRequest request,
        IReadOnlyList<SettlementItem> detailItems)
    {
        var amounts = new Dictionary<Guid, decimal>();
        if (request.OrderId is null)
        {
            return amounts;
        }

        var orderId = request.OrderId.Value;
        var orderType = request.OrderType;
        foreach (var item in detailItems)
        {
            if (item.OrderId == orderId && (orderType is null || item.OrderType == orderType.Value))
            {
                amounts[item.SettlementId] = amounts.GetValueOrDefault(item.SettlementId) + item.Amount;
            }
        }

        return amounts;
    }
}
