using App.Core.Abstractions;

namespace App.Core.Features.Reports.GetStockBalance;

/// <summary>
/// 库存余额表查询请求（Query 参数绑定；按分类聚合，不展开商品行）。
/// </summary>
public sealed class GetStockBalanceRequest : IRequest<ReportPageDto<StockBalanceItemDto, StockBalanceSummaryDto>>
{
    /// <summary>商品编码 / 名称关键词，可空</summary>
    public string? Keyword { get; init; }

    /// <summary>分类 id，可空</summary>
    public Guid? CategoryId { get; init; }

    /// <summary>页码，从 1 起</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; init; } = 20;
}
