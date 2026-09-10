using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Errors;

namespace App.Core.Features.Users.ResetPassword;

/// <summary>
/// 重置密码用例：校验存在性 → 哈希新密码 → 更新（管理员操作，不校验原密码）
/// </summary>
public sealed class ResetPasswordRequestHandler : IRequestHandler<ResetPasswordRequest, object?>
{
    private readonly IUserRepository _userRepository;
    private readonly PasswordHasher _passwordHasher;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// 初始化重置密码用例处理器
    /// </summary>
    public ResetPasswordRequestHandler(
        IUserRepository userRepository,
        PasswordHasher passwordHasher,
        ICurrentUser currentUser)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _currentUser = currentUser;
    }

    /// <summary>
    /// 处理重置密码请求
    /// </summary>
    /// <param name="request">重置密码请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>无业务数据（统一响应 data 为 null）</returns>
    public async Task<object?> HandleAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        // 查库约束：用户是否存在
        var user = await _userRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "用户不存在");

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.UpdatedAt = DateTimeOffset.UtcNow;
        user.UpdatedBy = UserInputNormalizer.CurrentUserId(_currentUser);

        await _userRepository.UpdateAsync(user, cancellationToken);
        return null;
    }
}
