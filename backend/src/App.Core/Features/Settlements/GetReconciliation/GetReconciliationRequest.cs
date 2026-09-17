using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Responses;

namespace App.Core.Features.Settlements.GetReconciliation;

/// <summary>
/// 往来对账台账查询请求（按往来单位聚合应收 / 应付余额与未结单据数）
/// </summary>
public sealed class GetReconciliationRequest : IRequest<PagedResult<ReconciliationListItemDto>>
{
    /// <summary>页码，从 1 起</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; init; } = 20;

    /// <summary>往来名称关键词，可空</summary>
    public string? Keyword { get; init; }

    /// <summary>往来单位类型（1 供应商 / 2 客户 / 3 两者），可空</summary>
    public PartnerType? Type { get; init; }
}
