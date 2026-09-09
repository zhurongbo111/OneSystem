using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Errors;
using App.Core.Features.Users;
using FluentValidation;

namespace App.Core.Features.Auth.Login;

/// <summary>
/// 登录用例处理器：格式校验 → 查库校验账号（业务约束）→ 签发 JWT。
/// 直接依赖仓储，无 Service 层；实现统一入口接口 <see cref="IRequestHandler{TRequest,TResponse}"/>。
/// </summary>
public sealed class LoginRequestHandler : IRequestHandler<LoginRequest, LoginResponse>
{
    private readonly IValidator<LoginRequest> _validator;
    private readonly IUserRepository _userRepository;
    private readonly TokenService _tokenService;

    /// <summary>
    /// 初始化登录用例处理器
    /// </summary>
    public LoginRequestHandler(IValidator<LoginRequest> validator, IUserRepository userRepository, TokenService tokenService)
    {
        _validator = validator;
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
        // 格式校验：只验证数据格式，失败抛 40000
        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            throw new BusinessException(ErrorCode.Validation, validation.Errors.First().ErrorMessage);
        }

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
