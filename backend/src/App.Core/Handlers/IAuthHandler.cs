using App.Core.Dtos;

namespace App.Core.Handlers;

/// <summary>
/// 认证 Handler：负责登录业务逻辑
/// </summary>
public interface IAuthHandler
{
    /// <summary>
    /// 登录：校验账号并签发 JWT
    /// </summary>
    /// <param name="request">登录请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}
