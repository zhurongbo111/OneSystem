using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Users;

namespace App.Core.Features.Auth.Login;

/// <summary>
/// 登录用例处理器：查库校验账号（密码哈希 + 账号状态，业务约束）→ 记录登录痕迹 → 签发 JWT。
/// 格式校验由 Mediator 分发前全局统一执行，本处理器不注入校验器。
/// 直接依赖仓储，无 Service 层；实现统一入口接口 <see cref="IRequestHandler{TRequest,TResponse}"/>。
/// </summary>
public sealed class LoginRequestHandler : IRequestHandler<LoginRequest, LoginResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserLoginLogRepository _userLoginLogRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly PasswordHasher _passwordHasher;
    private readonly TokenService _tokenService;
    private readonly IClientInfo _clientInfo;

    /// <summary>
    /// 初始化登录用例处理器
    /// </summary>
    public LoginRequestHandler(
        IUserRepository userRepository,
        IUserLoginLogRepository userLoginLogRepository,
        IUnitOfWork unitOfWork,
        PasswordHasher passwordHasher,
        TokenService tokenService,
        IClientInfo clientInfo)
    {
        _userRepository = userRepository;
        _userLoginLogRepository = userLoginLogRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _clientInfo = clientInfo;
    }

    /// <summary>
    /// 处理登录请求
    /// </summary>
    /// <param name="request">登录请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<LoginResponse> HandleAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        // 查库约束：账号与密码校验（需查询数据，故放 Handler 内）；用户名 / 密码错误统一提示，不区分
        var account = await _userRepository.GetByUsernameAsync(request.Username.Trim(), cancellationToken);
        if (account is null || !_passwordHasher.Verify(request.Password, account.PasswordHash))
        {
            throw new BusinessException(ErrorCode.LoginFailed, "用户名或密码错误");
        }

        // 禁用账号不允许登录（失败分支在写日志之前，故登录日志只记录成功登录）
        if (account.Status == UserStatus.Disabled)
        {
            throw new BusinessException(ErrorCode.UserDisabled, "账号已被禁用，请联系管理员");
        }

        var now = DateTimeOffset.UtcNow;

        // 跨仓储写操作：更新最近登录时间 + 追加登录日志，用工作单元保证原子性
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _userRepository.UpdateLastLoginAsync(account.Id, now, cancellationToken);
            await _userLoginLogRepository.AddAsync(
                new UserLoginLog
                {
                    Id = Guid.NewGuid(),
                    UserId = account.Id,
                    Username = account.Username,
                    DisplayName = account.DisplayName,
                    LoginAt = now,
                    IpAddress = _clientInfo.IpAddress,
                    UserAgent = _clientInfo.UserAgent,
                },
                cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        var user = new UserDto
        {
            Id = account.Id.ToString(),
            Username = account.Username,
            DisplayName = account.DisplayName,
        };
        return new LoginResponse { Token = _tokenService.Issue(user), User = user };
    }
}
