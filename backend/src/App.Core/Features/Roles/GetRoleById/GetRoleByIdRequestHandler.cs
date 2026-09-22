using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.Roles.GetRoleById;

/// <summary>
/// 角色详情用例：取角色 + 其权限点集合 + 绑定用户数（供抽屉回显）
/// </summary>
public sealed class GetRoleByIdRequestHandler : IRequestHandler<GetRoleByIdRequest, RoleDetailDto>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRoleRepository _userRoleRepository;

    /// <summary>
    /// 初始化角色详情用例处理器
    /// </summary>
    public GetRoleByIdRequestHandler(IRoleRepository roleRepository, IUserRoleRepository userRoleRepository)
    {
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
    }

    /// <summary>
    /// 处理角色详情请求
    /// </summary>
    /// <param name="request">详情请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<RoleDetailDto> HandleAsync(GetRoleByIdRequest request, CancellationToken cancellationToken = default)
    {
        var role = await _roleRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "角色不存在");

        var permissionKeys = await _roleRepository.GetPermissionKeysAsync(role.Id, cancellationToken);
        var userCount = await _userRoleRepository.CountByRoleAsync(role.Id, cancellationToken);

        return RoleDtoMapper.ToRoleDetailDto(role, permissionKeys, userCount);
    }
}
