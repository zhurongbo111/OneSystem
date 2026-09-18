using App.Core.Abstractions;

namespace App.Core.Features.Reports.GetCostProfitReport;

/// <summary>
/// 成本与毛利报表查询请求（Query 参数绑定；期间为左闭右开区间，口径见 `025` design §0.1）。
/// </summary>
public sealed class GetCostProfitReportRequest : IRequest<ReportPageDto<CostProfitItemDto, CostProfitTotalDto>>
{
    /// <summary>期间起（含，UTC），必填</summary>
    public DateTimeOffset? Start { get; init; }

    /// <summary>期间止（不含，UTC），必填</summary>
    public DateTimeOffset? End { get; init; }

    /// <summary>商品 id，可空</summary>
    public Guid? ProductId { get; init; }

    /// <summary>分类 id，可空</summary>
    public Guid? CategoryId { get; init; }

    /// <summary>分组维度（默认按单据）</summary>
    public CostProfitGroupBy GroupBy { get; init; } = CostProfitGroupBy.Order;

    /// <summary>页码，从 1 起</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; init; } = 20;
}
