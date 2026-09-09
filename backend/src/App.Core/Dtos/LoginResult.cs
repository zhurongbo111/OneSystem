namespace App.Core.Dtos;

/// <summary>
/// 登录结果
/// </summary>
public class LoginResult
{
    /// <summary>JWT token</summary>
    public string Token { get; init; } = string.Empty;

    /// <summary>当前用户信息</summary>
    public UserDto User { get; init; } = new();
}
