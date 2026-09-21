using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Responses;

namespace App.Core.Features.Settlements.GetSettlements;

/// <summary>
/// 收付款单分页查询用例：仓储分页筛选（含作废单据）→ 映射 DTO（含 Status 供前端置灰）。
/// 传入 orderType + orderId 时按被核销单据反查，并附上该单据在每张收付款单上的本次核销金额。
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

        // 按被核销单据反查时补齐「本次核销金额」（未指定单据则不查询，OrderAmount 为 null）
        var amountBySettlement = await GetOrderAmountsAsync(request, items, cancellationToken);

        return new PagedResult<SettlementListItemDto>
        {
            Items = items
                .Select(s => SettlementsDtoMapper.ToSettlementListItemDto(
                    s, amountBySettlement.TryGetValue(s.Id, out var amount) ? amount : null))
                .ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }

    /// <summary>
    /// 汇总每张收付款单对被核销单据的核销金额（未指定单据或本页为空时返回空表）。
    /// 复用既有批量取核销明细方法，避免逐单查询。
    /// </summary>
    private async Task<Dictionary<Guid, decimal>> GetOrderAmountsAsync(
        GetSettlementsRequest request,
        IReadOnlyList<Settlement> settlements,
        CancellationToken cancellationToken)
    {
        var amounts = new Dictionary<Guid, decimal>();
        if (request.OrderId is null || settlements.Count == 0)
        {
            return amounts;
        }

        var orderId = request.OrderId.Value;
        var orderType = request.OrderType;
        var items = await _settlementRepository.GetItemsBySettlementIdsAsync(
            settlements.Select(s => s.Id).ToList(), cancellationToken);

        foreach (var item in items)
        {
            if (item.OrderId == orderId && (orderType is null || item.OrderType == orderType.Value))
            {
                amounts[item.SettlementId] = amounts.GetValueOrDefault(item.SettlementId) + item.Amount;
            }
        }

        return amounts;
    }
}
