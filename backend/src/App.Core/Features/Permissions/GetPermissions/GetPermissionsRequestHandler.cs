using App.Core.Abstractions;

namespace App.Core.Features.Permissions.GetPermissions;

/// <summary>
/// 权限点分组清单用例：返回全部权限点分组（分组名 + key + 中文名），供角色授权界面渲染权限树。
/// 清单是代码常量（<see cref="App.Core.Auth.Permissions.Groups"/>），前端不硬编码。
/// </summary>
public sealed class GetPermissionsRequestHandler : IRequestHandler<GetPermissionsRequest, IReadOnlyList<PermissionGroupDto>>
{
    /// <summary>
    /// 处理权限点清单请求
    /// </summary>
    /// <param name="request">清单请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task<IReadOnlyList<PermissionGroupDto>> HandleAsync(
        GetPermissionsRequest request,
        CancellationToken cancellationToken = default)
    {
        // 本用例命名空间与权限常量类同名，此处必须完全限定引用（避免解析到命名空间）
        var groups = App.Core.Auth.Permissions.Groups
            .Select(g => new PermissionGroupDto
            {
                GroupName = g.Name,
                Items = g.Items.Select(i => new PermissionItemDto { Key = i.Key, Name = i.Name }).ToList(),
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<PermissionGroupDto>>(groups);
    }
}
