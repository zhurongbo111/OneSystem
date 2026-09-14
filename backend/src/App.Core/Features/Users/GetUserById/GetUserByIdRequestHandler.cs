using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.Users.GetUserById;

/// <summary>
/// 用户详情用例：按 id 查询单个用户，不存在抛 40400
/// </summary>
public sealed class GetUserByIdRequestHandler : IRequestHandler<GetUserByIdRequest, UserDetailDto>
{
    private readonly IUserRepository _userRepository;

    /// <summary>
    /// 初始化用户详情用例处理器
    /// </summary>
    public GetUserByIdRequestHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    /// <summary>
    /// 处理用户详情请求
    /// </summary>
    /// <param name="request">详情请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<UserDetailDto> HandleAsync(GetUserByIdRequest request, CancellationToken cancellationToken = default)
    {
        // 查库约束：用户是否存在
        var user = await _userRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "用户不存在");

        return UserDtoMapper.ToUserDetailDto(user);
    }
}
