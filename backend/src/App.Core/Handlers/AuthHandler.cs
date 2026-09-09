using App.Core.Auth;
using App.Core.Dtos;
using App.Core.Errors;
using App.Core.Services;

namespace App.Core.Handlers;

/// <summary>
/// 认证 Handler 实现
/// </summary>
public class AuthHandler : IAuthHandler
{
    private readonly IUserAccountService _accountService;
    private readonly TokenService _tokenService;

    /// <summary>
    /// 初始化认证 Handler
    /// </summary>
    public AuthHandler(IUserAccountService accountService, TokenService tokenService)
    {
        _accountService = accountService;
        _tokenService = tokenService;
    }

    /// <summary>
    /// 登录：参数校验 → 账号校验 → 签发 token
    /// </summary>
    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new BusinessException(ErrorCode.Validation, "用户名或密码不能为空");
        }

        var user = await _accountService.AuthenticateAsync(request.Username.Trim(), request.Password, cancellationToken);
        if (user is null)
        {
            throw new BusinessException(ErrorCode.LoginFailed, "用户名或密码错误");
        }

        return new LoginResult { Token = _tokenService.Issue(user), User = user };
    }
}
