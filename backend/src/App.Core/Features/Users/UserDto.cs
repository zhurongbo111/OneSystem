namespace App.Core.Features.Users;

/// <summary>
/// 用户出参模型（对外只暴露 Request/Response 模型，不暴露实体）。
/// 供"获取当前用户"与登录响应等处复用。
/// </summary>
public sealed class UserDto
{
    /// <summary>用户 ID</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>用户名</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>显示名称</summary>
    public string DisplayName { get; init; } = string.Empty;
}
