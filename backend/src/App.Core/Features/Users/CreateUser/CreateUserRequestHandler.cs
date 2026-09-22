using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Auth;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Users.CreateUser;

/// <summary>
/// 新增用户用例：校验唯一性（用户名 / 邮箱 / 手机号）→ 校验角色存在性 → 哈希密码 → 写入审计字段
/// → 同一事务内落库用户并全量替换其角色绑定
/// </summary>
public sealed class CreateUserRequestHandler : IRequestHandler<CreateUserRequest, UserDetailDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly PasswordHasher _passwordHasher;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化新增用户用例处理器
    /// </summary>
    public CreateUserRequestHandler(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IUserRoleRepository userRoleRepository,
        IUnitOfWork unitOfWork,
        PasswordHasher passwordHasher,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理新增用户请求
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<UserDetailDto> HandleAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var username = request.Username.Trim();
        var email = UserInputNormalizer.NormalizeEmail(request.Email);
        var phone = UserInputNormalizer.NullIfWhiteSpace(request.Phone);

        // 查库约束：唯一性（用户名大小写不敏感；邮箱 / 手机号仅对非空值判定）
        if (await _userRepository.ExistsByUsernameAsync(username, cancellationToken))
        {
            throw new BusinessException(ErrorCode.UsernameExists, "用户名已存在");
        }

        if (email is not null && await _userRepository.ExistsByEmailAsync(email, null, cancellationToken))
        {
            throw new BusinessException(ErrorCode.EmailExists, "邮箱已被使用");
        }

        if (phone is not null && await _userRepository.ExistsByPhoneAsync(phone, null, cancellationToken))
        {
            throw new BusinessException(ErrorCode.PhoneExists, "手机号已被使用");
        }

        // 查库约束：角色必须全部存在（去重后比对数量，避免部分命中被静默忽略）
        var roleIds = request.RoleIds.Distinct().ToList();
        var roles = await _roleRepository.GetByIdsAsync(roleIds, cancellationToken);
        if (roles.Count != roleIds.Count)
        {
            throw new BusinessException(ErrorCode.NotFound, "角色不存在");
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            PasswordHash = _passwordHasher.Hash(request.Password),
            DisplayName = request.DisplayName.Trim(),
            Email = email,
            Phone = phone,
            Status = UserStatus.Enabled,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        // 用户与其角色绑定必须同时成功，故由工作单元显式界定事务边界
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _userRepository.AddAsync(user, cancellationToken);
            await _userRoleRepository.ReplaceUserRolesAsync(user.Id, roleIds, cancellationToken);

            // 密码哈希不进 change 明细：字段名命中敏感黑名单（AuditChangeBuilder 兜底拦截）
            var createdUserChangeBuilder = new AuditChangeBuilder()
                .Add("username", "用户名", null, user.Username)
                .Add("displayName", "显示名称", null, user.DisplayName)
                .Add("email", "邮箱", null, user.Email)
                .Add("phone", "手机号", null, user.Phone)
                .Add("status", "状态", null, AuditText.UserStatus(user.Status))
                .Add("roleIds", "角色", null, AuditSummary.Join(roles.Select(r => r.Name)));
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.User,
                Action = AuditAction.Create,
                ResourceId = user.Id,
                ResourceNo = user.Username,
                Summary = $"新增用户 {user.DisplayName}（{user.Username}）角色：{AuditSummary.Join(roles.Select(r => r.Name))}",
                Changes = createdUserChangeBuilder.Build(),
                ChangesTruncated = createdUserChangeBuilder.Truncated,
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
}
