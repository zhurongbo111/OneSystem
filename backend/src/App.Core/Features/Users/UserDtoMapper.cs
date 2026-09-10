using App.Core.Entities;

namespace App.Core.Features.Users;

/// <summary>
/// 用户实体 → 出参模型 的映射（集中一处，避免各用例重复拼装）
/// </summary>
internal static class UserDtoMapper
{
    /// <summary>映射列表项出参</summary>
    public static UserListItemDto ToListItem(User user) => new()
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
    public static UserDetailDto ToDetail(User user) => new()
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
}
