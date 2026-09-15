using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Users.CreateUser;

/// <summary>
/// 新增用户用例：校验唯一性（用户名 / 邮箱 / 手机号）→ 哈希密码 → 写入审计字段 → 落库
/// </summary>
public sealed class CreateUserRequestHandler : IRequestHandler<CreateUserRequest, UserDetailDto>
{
    private readonly IUserRepository _userRepository;
    private readonly PasswordHasher _passwordHasher;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// 初始化新增用户用例处理器
    /// </summary>
    public CreateUserRequestHandler(
        IUserRepository userRepository,
        PasswordHasher passwordHasher,
        ICurrentUser currentUser)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _currentUser = currentUser;
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

        await _userRepository.AddAsync(user, cancellationToken);
        return UserDtoMapper.ToUserDetailDto(user);
    }
}
