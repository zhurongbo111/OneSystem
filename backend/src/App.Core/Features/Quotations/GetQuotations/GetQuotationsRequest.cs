using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Responses;

namespace App.Core.Features.Quotations.GetQuotations;

/// <summary>
/// 报价单分页查询请求（Query 参数绑定；只读）
/// </summary>
public sealed class GetQuotationsRequest : IRequest<PagedResult<QuotationListItemDto>>
{
    /// <summary>页码，从 1 起</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; init; } = 20;

    /// <summary>报价单号 / 客户名称关键词，可空</summary>
    public string? Keyword { get; init; }

    /// <summary>报价单状态，可空（0 草稿 / 1 已转订单 / 2 已作废）</summary>
    public QuotationStatus? Status { get; init; }

    /// <summary>起始报价日期（含），可空（前端传本地当天 00:00 的 UTC ISO 串）</summary>
    public DateTimeOffset? Start { get; init; }

    /// <summary>结束报价日期（含），可空（前端传本地当天 23:59:59 的 UTC ISO 串）</summary>
    public DateTimeOffset? End { get; init; }
}
