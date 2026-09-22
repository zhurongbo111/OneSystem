using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.Positions.GetPositions;

/// <summary>
/// 岗位分页列表请求（Query 参数绑定）
/// </summary>
public sealed class GetPositionsRequest : IRequest<PagedResult<PositionListItemDto>>
{
    /// <summary>关键词（编码 / 名称模糊匹配），可空</summary>
    public string? Keyword { get; init; }

    /// <summary>状态筛选（1 启用 / 0 停用），可空表示全部</summary>
    public int? Status { get; init; }

    /// <summary>页码，从 1 起</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; init; } = 20;
}
