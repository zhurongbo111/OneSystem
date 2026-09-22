using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Exports;

namespace App.Core.Features.Invoices.ExportInvoices;

/// <summary>
/// 发票导出请求（erp-export 续行）：筛选参数与列表查询一致；
/// 分页参数仅为与列表请求同形，导出取「当前筛选全量」，不参与分页（specs/027-erp-export/design.md §0.2）
/// </summary>
public sealed class ExportInvoicesRequest : IRequest<ExportResultDto>
{
    /// <summary>页码，从 1 起（导出忽略，仅为与列表参数一致）</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数（导出忽略，仅为与列表参数一致）</summary>
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