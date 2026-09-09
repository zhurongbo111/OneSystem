using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.Users.GetCurrentUser;

/// <summary>
/// 获取当前用户用例：由已认证的 <see cref="ICurrentUser"/> 还原出参模型。
/// 无请求参数，空请求 <see cref="GetCurrentUserRequest"/> 仅用于统一入口签名；出参复用共享模型 UserDto。
/// </summary>
public sealed class GetCurrentUserRequestHandler : IRequestHandler<GetCurrentUserRequest, UserDto>
{
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// 初始化当前用户用例处理器
    /// </summary>
    public GetCurrentUserRequestHandler(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    /// <summary>
    /// 获取当前登录用户（纯内存计算，直接返回结果）
    /// </summary>
    /// <param name="request">空请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task<UserDto> HandleAsync(GetCurrentUserRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_currentUser.Username))
        {
            throw new BusinessException(ErrorCode.Unauthorized, "用户信息缺失，请重新登录");
        }

        return Task.FromResult(new UserDto
        {
            Id = _currentUser.Id,
            Username = _currentUser.Username,
            DisplayName = _currentUser.DisplayName,
        });
    }
}
