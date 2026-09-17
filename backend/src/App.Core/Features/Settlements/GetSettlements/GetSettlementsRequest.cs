using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Responses;

namespace App.Core.Features.Settlements.GetSettlements;

/// <summary>
/// 收付款单分页查询请求（Query 参数绑定；只读）
/// </summary>
public sealed class GetSettlementsRequest : IRequest<PagedResult<SettlementListItemDto>>
{
    /// <summary>页码，从 1 起</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; init; } = 20;

    /// <summary>单号 / 往来名称关键词，可空</summary>
    public string? Keyword { get; init; }

    /// <summary>类型（0 收款 / 1 付款），可空</summary>
    public SettlementType? Type { get; init; }

    /// <summary>往来单位 id，可空</summary>
    public Guid? PartnerId { get; init; }

    /// <summary>方式（0 现金 / 1 银行转账 / 2 其他），可空</summary>
    public SettlementMethod? Method { get; init; }

    /// <summary>起始业务日期（含），可空（前端传本地当天 00:00 的 UTC ISO 串）</summary>
    public DateTimeOffset? Start { get; init; }

    /// <summary>结束业务日期（含），可空（前端传本地当天 23:59:59 的 UTC ISO 串）</summary>
    public DateTimeOffset? End { get; init; }
}
