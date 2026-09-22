using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.Roles.GetRoles;

/// <summary>
/// 角色分页列表用例：按关键词模糊筛选后分页查询，直接依赖仓储，无 Service 层
/// </summary>
public sealed class GetRolesRequestHandler : IRequestHandler<GetRolesRequest, PagedResult<RoleListItemDto>>
{
    private readonly IRoleRepository _roleRepository;

    /// <summary>
    /// 初始化角色分页列表用例处理器
    /// </summary>
    public GetRolesRequestHandler(IRoleRepository roleRepository)
    {
        _roleRepository = roleRepository;
    }

    /// <summary>
    /// 处理角色分页列表请求
    /// </summary>
    /// <param name="request">列表请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<RoleListItemDto>> HandleAsync(GetRolesRequest request, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _roleRepository.GetPagedAsync(
            request.Keyword,
            request.Page,
            request.PageSize,
            cancellationToken);

        return new PagedResult<RoleListItemDto>
        {
            Items = items.Select(RoleDtoMapper.ToRoleListItemDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
