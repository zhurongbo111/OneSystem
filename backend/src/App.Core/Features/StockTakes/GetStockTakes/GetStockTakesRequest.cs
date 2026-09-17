using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Responses;

namespace App.Core.Features.StockTakes.GetStockTakes;

/// <summary>
/// 盘点单分页查询请求（Query 参数绑定；只读）
/// </summary>
public sealed class GetStockTakesRequest : IRequest<PagedResult<StockTakeListItemDto>>
{
    /// <summary>页码，从 1 起</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; init; } = 20;

    /// <summary>单号关键词，可空（匹配 TakeNo）</summary>
    public string? Keyword { get; init; }

    /// <summary>单据类型（0 期初建账 / 1 库存盘点），可空</summary>
    public StockTakeType? Type { get; init; }

    /// <summary>起始盘点日期（含），可空（前端传本地当天 00:00 的 UTC ISO 串）</summary>
    public DateTimeOffset? Start { get; init; }

    /// <summary>结束盘点日期（含），可空（前端传本地当天 23:59:59 的 UTC ISO 串）</summary>
    public DateTimeOffset? End { get; init; }
}
