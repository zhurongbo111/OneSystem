using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.Users.GetUsers;

/// <summary>
/// 用户分页列表请求（Query 参数绑定）
/// </summary>
public sealed class GetUsersRequest : IRequest<PagedResult<UserListItemDto>>
{
    /// <summary>关键词（用户名 / 显示名模糊匹配），可空</summary>
    public string? Keyword { get; init; }

    /// <summary>状态筛选（1 启用 / 0 禁用），可空表示全部</summary>
    public int? Status { get; init; }

    /// <summary>页码，从 1 起</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; init; } = 20;
}
