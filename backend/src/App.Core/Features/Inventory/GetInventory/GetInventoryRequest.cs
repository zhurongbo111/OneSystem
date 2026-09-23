using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.Inventory.GetInventory;

/// <summary>
/// 库存分页查询请求（只读；仅启用商品，过滤在仓储内完成）
/// </summary>
public sealed class GetInventoryRequest : IRequest<PagedResult<InventoryItemDto>>
{
    /// <summary>页码，从 1 起</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; init; } = 20;

    /// <summary>关键词（编码 / 名称模糊匹配），可空</summary>
    public string? Keyword { get; init; }

    /// <summary>分类 id，可空</summary>
    public Guid? CategoryId { get; init; }

    /// <summary>仓库 id，可空（不传 = 全部仓，038）</summary>
    public Guid? WarehouseId { get; init; }
}
