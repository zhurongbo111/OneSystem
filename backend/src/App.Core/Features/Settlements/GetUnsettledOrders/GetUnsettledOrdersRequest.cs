using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Responses;

namespace App.Core.Features.Settlements.GetUnsettledOrders;

/// <summary>
/// 可核销单据候选查询请求（新建收付款单页：按往来单位 + 方向返回未结单据）
/// </summary>
public sealed class GetUnsettledOrdersRequest : IRequest<PagedResult<SettlementCandidateDto>>
{
    /// <summary>往来单位 id</summary>
    public required Guid PartnerId { get; init; }

    /// <summary>收付款类型（0 收款 / 1 付款；决定可核销的单据类型集合）</summary>
    public required SettlementType Type { get; init; }

    /// <summary>页码，从 1 起</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; init; } = 20;
}
