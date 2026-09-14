using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Users;

/// <summary>
/// 用户实体 → 出参模型 的映射（集中一处，避免各用例重复拼装）
/// </summary>
internal static class UserDtoMapper
{
    /// <summary>映射列表项出参</summary>
    public static UserListItemDto ToUserListItemDto(User user) => new()
    {
        Id = user.Id.ToString(),
        Username = user.Username,
        DisplayName = user.DisplayName,
        Email = user.Email,
        Phone = user.Phone,
        Status = (int)user.Status,
        LastLoginAt = user.LastLoginAt,
        CreatedAt = user.CreatedAt,
    };

    /// <summary>映射详情出参</summary>
    public static UserDetailDto ToUserDetailDto(User user) => new()
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
}
