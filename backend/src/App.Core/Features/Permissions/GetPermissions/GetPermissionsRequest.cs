using App.Core.Abstractions;

namespace App.Core.Features.Permissions.GetPermissions;

/// <summary>
/// 权限点分组清单请求（无参数）：数据源为代码常量 <c>App.Core.Auth.Permissions</c>
/// </summary>
public sealed class GetPermissionsRequest : IRequest<IReadOnlyList<PermissionGroupDto>>
{
}
