using App.Core.Abstractions;

namespace App.Core.Features.Users.GetMyPermissions;

/// <summary>
/// 当前登录用户权限点集合请求（无参数）：
/// 接口属免权限校验白名单（<c>specs/028-erp-rbac/design.md</c> §0.4），否则会形成「需要权限才能查权限」死锁。
/// </summary>
public sealed class GetMyPermissionsRequest : IRequest<IReadOnlyList<string>>
{
}
