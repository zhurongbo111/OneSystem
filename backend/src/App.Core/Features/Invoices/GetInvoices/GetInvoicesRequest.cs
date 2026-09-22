using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Responses;

namespace App.Core.Features.Invoices.GetInvoices;

/// <summary>
/// 发票分页查询请求（Query 参数绑定；只读）
/// </summary>
public sealed class GetInvoicesRequest : IRequest<PagedResult<InvoiceListItemDto>>
{
    /// <summary>页码，从 1 起</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; init; } = 20;

    /// <summary>发票号 / 往来名称 / 关联单据号关键词，可空</summary>
    public string? Keyword { get; init; }

    /// <summary>发票类型（0 进项 / 1 销项），可空</summary>
    public InvoiceType? Type { get; init; }

    /// <summary>往来单位 id，可空</summary>
    public Guid? PartnerId { get; init; }

    /// <summary>起始开票日期（含），可空（前端传本地当天 00:00 的 UTC ISO 串）</summary>
    public DateTimeOffset? Start { get; init; }

    /// <summary>结束开票日期（含），可空（前端传本地当天 23:59:59 的 UTC ISO 串）</summary>
    public DateTimeOffset? End { get; init; }
}