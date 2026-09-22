using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Users.UpdateUser;

/// <summary>
/// 编辑用户用例：校验存在性 + 邮箱 / 手机号唯一性（排除自身）→ 角色存在性 →
/// 同一事务内更新展示类字段并全量替换用户角色绑定
/// </summary>
public sealed class UpdateUserRequestHandler : IRequestHandler<UpdateUserRequest, UserDetailDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化编辑用户用例处理器
    /// </summary>
    public UpdateUserRequestHandler(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IUserRoleRepository userRoleRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理编辑用户请求
    /// </summary>
    /// <param name="request">编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<UserDetailDto> HandleAsync(UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        // 查库约束：用户是否存在
        var user = await _userRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "用户不存在");

        var email = UserInputNormalizer.NormalizeEmail(request.Email);
        var phone = UserInputNormalizer.NullIfWhiteSpace(request.Phone);

        // 查库约束：唯一性需排除自身
        if (email is not null && await _userRepository.ExistsByEmailAsync(email, user.Id, cancellationToken))
        {
            throw new BusinessException(ErrorCode.EmailExists, "邮箱已被使用");
        }

        if (phone is not null && await _userRepository.ExistsByPhoneAsync(phone, user.Id, cancellationToken))
        {
            throw new BusinessException(ErrorCode.PhoneExists, "手机号已被使用");
        }

        // 查库约束：角色必须全部存在（去重后比对数量）
        var roleIds = request.RoleIds.Distinct().ToList();
        var roles = await _roleRepository.GetByIdsAsync(roleIds, cancellationToken);
        if (roles.Count != roleIds.Count)
        {
            throw new BusinessException(ErrorCode.NotFound, "角色不存在");
        }

        // 变更前后快照：角色是全量替换，需先取原绑定才能还原「谁的角色被改了」
        var beforeRoleNames = await GetRoleNamesAsync(user.Id, cancellationToken);
        var beforeDisplayName = user.DisplayName;
        var beforeEmail = user.Email;
        var beforePhone = user.Phone;
        var now = DateTimeOffset.UtcNow;

        user.DisplayName = request.DisplayName.Trim();
        user.Email = email;
        user.Phone = phone;
        user.UpdatedAt = now;
        user.UpdatedBy = _currentUser.UserId();

        var changeBuilder = new AuditChangeBuilder()
            .Add("displayName", "显示名称", beforeDisplayName, user.DisplayName)
            .Add("email", "邮箱", beforeEmail, user.Email)
            .Add("phone", "手机号", beforePhone, user.Phone)
            .Add(
                "roleIds",
                "角色",
                AuditSummary.Join(beforeRoleNames),
                AuditSummary.Join(roles.Select(r => r.Name)));

        // 用户更新与角色绑定替换必须同时成功，故由工作单元显式界定事务边界
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _userRepository.UpdateAsync(user, cancellationToken);
            await _userRoleRepository.ReplaceUserRolesAsync(user.Id, roleIds, cancellationToken);

            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.User,
                Action = AuditAction.Update,
                ResourceId = user.Id,
                ResourceNo = user.Username,
                Summary = $"编辑用户 {user.DisplayName}（{user.Username}）角色：{AuditSummary.Join(beforeRoleNames)} → {AuditSummary.Join(roles.Select(r => r.Name))}",
                Changes = changeBuilder.Build(),
                ChangesTruncated = changeBuilder.Truncated,
                UtcNow = now,
            }, cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        var roleItems = roles.Select(r => new UserRoleItem { Id = r.Id, Name = r.Name }).ToList();
        return UserDtoMapper.ToUserDetailDto(user, UserDtoMapper.ToUserRoleDtos(roleItems));
    }

    /// <summary>读取用户当前绑定的角色名（变更前后比对用）</summary>
    private async Task<IReadOnlyList<string>> GetRoleNamesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rolesByUser = await _userRoleRepository.GetRolesByUserIdsAsync([userId], cancellationToken);
        return rolesByUser.TryGetValue(userId, out var items)
            ? items.Select(x => x.Name).Where(name => !string.IsNullOrWhiteSpace(name)).ToList()
            : [];
    }
}
