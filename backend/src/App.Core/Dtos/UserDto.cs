namespace App.Core.Dtos;

/// <summary>
/// 用户信息（对外只暴露 DTO，不暴露实体）
/// </summary>
public class UserDto
{
    /// <summary>用户 ID</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>用户名</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>显示名称</summary>
    public string DisplayName { get; init; } = string.Empty;
}
