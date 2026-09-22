using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Users;

/// <summary>
/// 用户实体 → 出参模型 的映射（集中一处，避免各用例重复拼装）。
/// 角色集合不随用户实体加载，由用例经 <c>IUserRoleRepository</c> 单独取得后一并传入。
/// </summary>
internal static class UserDtoMapper
{
    /// <summary>映射列表项出参</summary>
    /// <param name="user">用户实体</param>
    /// <param name="roles">用户绑定角色（按名称升序）</param>
    public static UserListItemDto ToUserListItemDto(User user, IReadOnlyList<UserRoleDto> roles) => new()
    {
        Id = user.Id.ToString(),
        Username = user.Username,
        DisplayName = user.DisplayName,
        Email = user.Email,
        Phone = user.Phone,
        Status = (int)user.Status,
        LastLoginAt = user.LastLoginAt,
        CreatedAt = user.CreatedAt,
        Roles = roles,
    };

    /// <summary>映射详情出参</summary>
    /// <param name="user">用户实体</param>
    /// <param name="roles">用户绑定角色（按名称升序）</param>
    public static UserDetailDto ToUserDetailDto(User user, IReadOnlyList<UserRoleDto> roles) => new()
    {
        Id = user.Id.ToString(),
        Username = user.Username,
        DisplayName = user.DisplayName,
        Email = user.Email,
        Phone = user.Phone,
        Status = (int)user.Status,
        LastLoginAt = user.LastLoginAt,
        CreatedAt = user.CreatedAt,
        UpdatedAt = user.UpdatedAt,
        Roles = roles,
    };

    /// <summary>映射当前用户出参（登录复用，由用户实体取基础字段）</summary>
    public static UserDto ToUserDto(User user) => new()
    {
        Id = user.Id.ToString(),
        Username = user.Username,
        DisplayName = user.DisplayName,
    };

    /// <summary>映射当前用户出参（获取当前用户复用，直接取自已认证的当前用户上下文）</summary>
    public static UserDto ToUserDto(ICurrentUser currentUser) => new()
    {
        Id = currentUser.Id,
        Username = currentUser.Username,
        DisplayName = currentUser.DisplayName,
    };

    /// <summary>映射仓储读模型 → 用户角色出参（按名称升序，保证回显顺序稳定）</summary>
    /// <param name="roles">用户绑定的角色读模型</param>
    public static IReadOnlyList<UserRoleDto> ToUserRoleDtos(IEnumerable<UserRoleItem> roles)
        => roles
            .OrderBy(x => x.Name)
            .Select(x => new UserRoleDto { Id = x.Id.ToString(), Name = x.Name })
            .ToList();
}
