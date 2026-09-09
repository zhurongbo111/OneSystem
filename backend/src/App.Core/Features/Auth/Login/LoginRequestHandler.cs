using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Errors;
using App.Core.Features.Users;

namespace App.Core.Features.Auth.Login;

/// <summary>
/// 登录用例处理器：查库校验账号（业务约束）→ 签发 JWT。
/// 格式校验由 Mediator 分发前全局统一执行，本处理器不注入校验器。
/// 直接依赖仓储，无 Service 层；实现统一入口接口 <see cref="IRequestHandler{TRequest,TResponse}"/>。
/// </summary>
public sealed class LoginRequestHandler : IRequestHandler<LoginRequest, LoginResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly TokenService _tokenService;

    /// <summary>
    /// 初始化登录用例处理器
    /// </summary>
    public LoginRequestHandler(IUserRepository userRepository, TokenService tokenService)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
    }

    /// <summary>
    /// 处理登录请求
    /// </summary>
    /// <param name="request">登录请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<LoginResponse> HandleAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        // 查库约束：账号与密码校验（需查询数据，故放 Handler 内）
        var account = await _userRepository.GetByUsernameAsync(request.Username.Trim(), cancellationToken);
        if (account is null || !string.Equals(account.Password, request.Password, StringComparison.Ordinal))
        {
            throw new BusinessException(ErrorCode.LoginFailed, "用户名或密码错误");
        }

        var user = new UserDto { Id = account.Id, Username = account.Username, DisplayName = account.DisplayName };
        return new LoginResponse { Token = _tokenService.Issue(user), User = user };
    }
}
