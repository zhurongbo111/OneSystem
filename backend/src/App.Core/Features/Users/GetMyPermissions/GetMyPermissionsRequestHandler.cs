using App.Core.Abstractions;

namespace App.Core.Features.Users.GetMyPermissions;

/// <summary>
/// 当前登录用户权限点集合用例：经 <see cref="IPermissionResolver"/> 求用户 → 角色 → 权限点并集。
/// </summary>
public sealed class GetMyPermissionsRequestHandler : IRequestHandler<GetMyPermissionsRequest, IReadOnlyList<string>>
{
    private readonly IPermissionResolver _permissionResolver;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// 初始化当前用户权限集合用例处理器
    /// </summary>
    public GetMyPermissionsRequestHandler(IPermissionResolver permissionResolver, ICurrentUser currentUser)
    {
        _permissionResolver = permissionResolver;
        _currentUser = currentUser;
    }

    /// <summary>
    /// 处理当前用户权限集合请求
    /// </summary>
    /// <param name="request">权限集合请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<IReadOnlyList<string>> HandleAsync(
        GetMyPermissionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId();
        if (userId is null)
        {
            // 该接口需先通过认证管道（无有效 token 时不会产生本分支），此处兜底返回空集
            return [];
        }

        var permissions = await _permissionResolver.GetPermissionsAsync(userId.Value, cancellationToken);
        return permissions.ToList();
    }
}
