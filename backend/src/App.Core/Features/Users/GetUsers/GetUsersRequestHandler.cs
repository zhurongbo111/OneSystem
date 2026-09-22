using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Responses;

namespace App.Core.Features.Users.GetUsers;

/// <summary>
/// 用户分页列表用例：按关键词 / 状态筛选后分页查询，直接依赖仓储，无 Service 层。
/// 角色集合按用户 id 批量取数（一次查询），避免逐行查询的 N+1。
/// </summary>
public sealed class GetUsersRequestHandler : IRequestHandler<GetUsersRequest, PagedResult<UserListItemDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserRoleRepository _userRoleRepository;

    /// <summary>
    /// 初始化用户分页列表用例处理器
    /// </summary>
    public GetUsersRequestHandler(IUserRepository userRepository, IUserRoleRepository userRoleRepository)
    {
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
    }

    /// <summary>
    /// 处理用户分页列表请求
    /// </summary>
    /// <param name="request">列表请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<UserListItemDto>> HandleAsync(GetUsersRequest request, CancellationToken cancellationToken = default)
    {
        var status = request.Status is null ? (UserStatus?)null : (UserStatus)request.Status.Value;
        var (users, total) = await _userRepository.GetPagedAsync(
            request.Keyword,
            status,
            request.Page,
            request.PageSize,
            cancellationToken);

        var rolesByUser = await _userRoleRepository.GetRolesByUserIdsAsync(
            users.Select(u => u.Id).ToList(),
            cancellationToken);

        var items = users
            .Select(u => UserDtoMapper.ToUserListItemDto(
                u,
                rolesByUser.TryGetValue(u.Id, out var roles) ? UserDtoMapper.ToUserRoleDtos(roles) : []))
            .ToList();

        return new PagedResult<UserListItemDto>
        {
            Items = items,
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
