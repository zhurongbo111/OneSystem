using App.Core.Abstractions;

namespace App.Core.Features.Reports.GetPurchaseSummary;

/// <summary>
/// 采购汇总查询请求（Query 参数绑定；期间为左闭右开区间，退货与入库分列，净额由出参计算）。
/// </summary>
public sealed class GetPurchaseSummaryRequest : IRequest<ReportPageDto<PurchaseSummaryItemDto, PurchaseSummaryTotalDto>>
{
    /// <summary>期间起（含，UTC），必填</summary>
    public DateTimeOffset? Start { get; init; }

    /// <summary>期间止（不含，UTC），必填</summary>
    public DateTimeOffset? End { get; init; }

    /// <summary>供应商 id，可空</summary>
    public Guid? PartnerId { get; init; }

    /// <summary>分组维度（默认按往来单位）</summary>
    public SummaryGroupBy GroupBy { get; init; } = SummaryGroupBy.Partner;

    /// <summary>页码，从 1 起</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; init; } = 20;
}
