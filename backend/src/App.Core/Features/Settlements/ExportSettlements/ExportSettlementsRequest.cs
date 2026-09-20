using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Exports;

namespace App.Core.Features.Settlements.ExportSettlements;

/// <summary>
/// 收付款单导出请求（erp-export）：筛选参数与列表查询一致；
/// 分页参数仅为与列表请求同形，导出取「当前筛选全量」，不参与分页（specs/027-erp-export/design.md §0.2）
/// </summary>
public sealed class ExportSettlementsRequest : IRequest<ExportResultDto>
{
    /// <summary>页码，从 1 起（导出忽略，仅为与列表参数一致）</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数（导出忽略，仅为与列表参数一致）</summary>
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
