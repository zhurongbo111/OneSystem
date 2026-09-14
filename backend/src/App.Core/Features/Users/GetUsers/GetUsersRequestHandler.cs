using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Responses;

namespace App.Core.Features.Users.GetUsers;

/// <summary>
/// 用户分页列表用例：按关键词 / 状态筛选后分页查询，直接依赖仓储，无 Service 层
/// </summary>
public sealed class GetUsersRequestHandler : IRequestHandler<GetUsersRequest, PagedResult<UserListItemDto>>
{
    private readonly IUserRepository _userRepository;

    /// <summary>
    /// 初始化用户分页列表用例处理器
    /// </summary>
    public GetUsersRequestHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    /// <summary>
    /// 处理用户分页列表请求
    /// </summary>
    /// <param name="request">列表请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<UserListItemDto>> HandleAsync(GetUsersRequest request, CancellationToken cancellationToken = default)
    {
        var status = request.Status is null ? (UserStatus?)null : (UserStatus)request.Status.Value;
        var (items, total) = await _userRepository.GetPagedAsync(
            request.Keyword,
            status,
            request.Page,
            request.PageSize,
            cancellationToken);

        return new PagedResult<UserListItemDto>
        {
            Items = items.Select(UserDtoMapper.ToUserListItemDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
