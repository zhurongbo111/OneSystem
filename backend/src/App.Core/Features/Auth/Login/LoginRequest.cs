using App.Core.Abstractions;

namespace App.Core.Features.Auth.Login;

/// <summary>
/// 登录请求（实现 <see cref="IRequest{TResponse}"/> 标记，供 IMediator 分发）
/// </summary>
public sealed class LoginRequest : IRequest<LoginResponse>
{
    /// <summary>用户名</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>密码</summary>
    public string Password { get; init; } = string.Empty;
}
