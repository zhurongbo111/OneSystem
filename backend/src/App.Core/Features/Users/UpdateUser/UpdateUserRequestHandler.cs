using App.Core.Abstractions;
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

    /// <summary>
    /// 初始化编辑用户用例处理器
    /// </summary>
    public UpdateUserRequestHandler(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IUserRoleRepository userRoleRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
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

        user.DisplayName = request.DisplayName.Trim();
        user.Email = email;
        user.Phone = phone;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        user.UpdatedBy = _currentUser.UserId();

        // 用户更新与角色绑定替换必须同时成功，故由工作单元显式界定事务边界
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _userRepository.UpdateAsync(user, cancellationToken);
            await _userRoleRepository.ReplaceUserRolesAsync(user.Id, roleIds, cancellationToken);
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
