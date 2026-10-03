using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.Batches.GetBatches;

/// <summary>
/// 批次分页查询请求（批次号关键词 / 商品 / 状态 / 仅看近效期或过期筛选）
/// </summary>
public sealed class GetBatchesRequest : IRequest<PagedResult<BatchListItemDto>>
{
    /// <summary>批次号关键词（模糊匹配）</summary>
    public string? Keyword { get; init; }

    /// <summary>商品 id（精确匹配，可空）</summary>
    public Guid? ProductId { get; init; }

    /// <summary>批次状态（0 停用 / 1 启用，可空 = 全部）</summary>
    public int? Status { get; init; }

    /// <summary>是否只看近效期 / 已过期（可空）</summary>
    public bool? OnlyExpiring { get; init; }

    /// <summary>页码（从 1 起）</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数（默认 20，上限 100）</summary>
    public int PageSize { get; init; } = 20;
}
