using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Users.UpdateUserStatus;

/// <summary>
/// 启用 / 禁用用户用例：校验存在性，并禁止禁用当前登录账号（避免自锁）
/// </summary>
public sealed class UpdateUserStatusRequestHandler : IRequestHandler<UpdateUserStatusRequest, UserDetailDto>
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// 初始化启用 / 禁用用户用例处理器
    /// </summary>
    public UpdateUserStatusRequestHandler(IUserRepository userRepository, ICurrentUser currentUser)
    {
        _userRepository = userRepository;
        _currentUser = currentUser;
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

        user.Status = targetStatus;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        user.UpdatedBy = _currentUser.UserId();

        await _userRepository.UpdateAsync(user, cancellationToken);
        return UserDtoMapper.ToUserDetailDto(user);
    }
}
