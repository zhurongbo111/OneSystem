using Microsoft.AspNetCore.Mvc.Filters;

namespace App.Api.Authorization;

/// <summary>
/// 权限校验豁免标注：用于「只要登录即可访问」的动作（如 <c>GET /api/users/me</c>、<c>GET /api/users/me/permissions</c>）。
/// 由 <see cref="PermissionAuthorizationFilter"/> 识别后放行；豁免清单属字节级决策，
/// 新增用例须同步 <c>specs/028-erp-rbac/design.md</c> §3.2 白名单说明。
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class SkipPermissionCheckAttribute : Attribute, IFilterMetadata
{
}
