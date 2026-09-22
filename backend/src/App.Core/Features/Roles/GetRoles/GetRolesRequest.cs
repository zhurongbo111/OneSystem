using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.Roles.GetRoles;

/// <summary>
/// 角色分页列表请求（Query 参数绑定）
/// </summary>
public sealed class GetRolesRequest : IRequest<PagedResult<RoleListItemDto>>
{
    /// <summary>关键词（角色名称 / 备注模糊匹配），可空</summary>
    public string? Keyword { get; init; }

    /// <summary>页码，从 1 起</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; init; } = 20;
}
