using App.Core.Features.Users;

namespace App.Core.Features.Auth.Login;

/// <summary>
/// 登录响应（即接口 `data` 的模型）
/// </summary>
public sealed class LoginResponse
{
    /// <summary>JWT token</summary>
    public string Token { get; init; } = string.Empty;

    /// <summary>当前登录用户</summary>
    public UserDto User { get; init; } = new();

    /// <summary>
    /// 当前用户的权限点 key 集合（登录即返回，前端据此过滤菜单与按钮；
    /// 后续可由 <c>GET /api/users/me/permissions</c> 刷新，避免重新登录）
    /// </summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];
}
