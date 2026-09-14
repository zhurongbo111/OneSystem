using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.Users.UpdateUser;

/// <summary>
/// 编辑用户用例：校验存在性 + 邮箱 / 手机号唯一性（排除自身）→ 更新展示类字段
/// </summary>
public sealed class UpdateUserRequestHandler : IRequestHandler<UpdateUserRequest, UserDetailDto>
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// 初始化编辑用户用例处理器
    /// </summary>
    public UpdateUserRequestHandler(IUserRepository userRepository, ICurrentUser currentUser)
    {
        _userRepository = userRepository;
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

        user.DisplayName = request.DisplayName.Trim();
        user.Email = email;
        user.Phone = phone;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        user.UpdatedBy = UserInputNormalizer.CurrentUserId(_currentUser);

        await _userRepository.UpdateAsync(user, cancellationToken);
        return UserDtoMapper.ToUserDetailDto(user);
    }
}
