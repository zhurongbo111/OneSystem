using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Responses;

namespace App.Core.Features.StockMovements.GetStockMovements;

/// <summary>
/// 库存流水分页查询请求（Query 参数绑定；只读，按变动时间倒序）
/// </summary>
public sealed class GetStockMovementsRequest : IRequest<PagedResult<StockMovementListItemDto>>
{
    /// <summary>来源单号关键词（模糊匹配），可空</summary>
    public string? Keyword { get; init; }

    /// <summary>商品 id，可空</summary>
    public Guid? ProductId { get; init; }

    /// <summary>仓库 id，可空（038；不传 = 全部仓）</summary>
    public Guid? WarehouseId { get; init; }

    /// <summary>变动类型，可空</summary>
    public StockMovementType? Type { get; init; }

    /// <summary>变动时间起（含，UTC），可空</summary>
    public DateTimeOffset? Start { get; init; }

    /// <summary>变动时间止（含，UTC），可空</summary>
    public DateTimeOffset? End { get; init; }

    /// <summary>页码，从 1 起</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; init; } = 20;
}
