using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Auth;
using App.Core.Entities;
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
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化重置密码用例处理器
    /// </summary>
    public ResetPasswordRequestHandler(
        IUserRepository userRepository,
        PasswordHasher passwordHasher,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
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

        var now = DateTimeOffset.UtcNow;
        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.UpdatedAt = now;
        user.UpdatedBy = _currentUser.UserId();

        await _userRepository.UpdateAsync(user, cancellationToken);

        // change 明细为空：新密码与哈希均命中敏感黑名单，只留"谁被谁重置了"的摘要
        await _auditLogger.RecordAsync(new AuditEntry
        {
            Resource = AuditResource.User,
            Action = AuditAction.Update,
            ResourceId = user.Id,
            ResourceNo = user.Username,
            Summary = $"重置用户密码 {user.DisplayName}（{user.Username}）",
            UtcNow = now,
        }, cancellationToken);

        return null;
    }
}
