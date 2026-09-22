using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Users.UpdateUserStatus;

/// <summary>
/// 启用 / 禁用用户用例：校验存在性，并禁止禁用当前登录账号（避免自锁）
/// </summary>
public sealed class UpdateUserStatusRequestHandler : IRequestHandler<UpdateUserStatusRequest, UserDetailDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化启用 / 禁用用户用例处理器
    /// </summary>
    public UpdateUserStatusRequestHandler(
        IUserRepository userRepository,
        IUserRoleRepository userRoleRepository,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理启用 / 禁用用户请求
    /// </summary>
    /// <param name="request">状态变更请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<UserDetailDto> HandleAsync(UpdateUserStatusRequest request, CancellationToken cancellationToken = default)
    {
        // 查库约束：用户是否存在
        var user = await _userRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "用户不存在");

        var targetStatus = (UserStatus)request.Status;

        // 业务约束：不能禁用自己，否则会立即失去后台入口
        if (targetStatus == UserStatus.Disabled
            && _currentUser.UserId() == user.Id)
        {
            throw new BusinessException(ErrorCode.CannotDisableSelf, "不能禁用当前登录账号");
        }

        var beforeStatus = user.Status;
        var now = DateTimeOffset.UtcNow;

        user.Status = targetStatus;
        user.UpdatedAt = now;
        user.UpdatedBy = _currentUser.UserId();

        await _userRepository.UpdateAsync(user, cancellationToken);

        var statusChangeBuilder = new AuditChangeBuilder()
            .Add("status", "状态", AuditText.UserStatus(beforeStatus), AuditText.UserStatus(user.Status));
        await _auditLogger.RecordAsync(new AuditEntry
        {
            Resource = AuditResource.User,
            Action = AuditAction.StatusChange,
            ResourceId = user.Id,
            ResourceNo = user.Username,
            Summary = $"{(user.Status == UserStatus.Enabled ? "启用" : "禁用")}用户 {user.DisplayName}（{user.Username}）",
            Changes = statusChangeBuilder.Build(),
            ChangesTruncated = statusChangeBuilder.Truncated,
            UtcNow = now,
        }, cancellationToken);

        var rolesByUser = await _userRoleRepository.GetRolesByUserIdsAsync([user.Id], cancellationToken);
        var roles = rolesByUser.TryGetValue(user.Id, out var items) ? UserDtoMapper.ToUserRoleDtos(items) : [];

        return UserDtoMapper.ToUserDetailDto(user, roles);
    }
}
