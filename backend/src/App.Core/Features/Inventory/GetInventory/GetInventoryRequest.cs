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

    /// <summary>批次 id，可空（040：按批次筛选）</summary>
    public Guid? BatchId { get; init; }

    /// <summary>批次号，可空（040：按批次号模糊筛选，大小写不敏感；与 <see cref="BatchId"/> 同时提供时取交集）</summary>
    public string? BatchNo { get; init; }

    /// <summary>是否按批次展开行（040，默认 false = 按「商品 × 仓」汇总）</summary>
    public bool ExpandBatch { get; init; }
}
