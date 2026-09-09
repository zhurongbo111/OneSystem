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
}
