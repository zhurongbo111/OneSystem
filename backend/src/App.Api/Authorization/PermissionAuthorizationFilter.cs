using App.Core.Abstractions;
using App.Core.Errors;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace App.Api.Authorization;

/// <summary>
/// 全局权限校验过滤器：用户 → 角色 → 权限点，纯内存比对。
/// 与 JWT 认证三条分流线一致（AGENTS.md §4.6）：无权限统一 HTTP 200 + <c>code = 40300</c>，不返回 403。
/// </summary>
/// <remarks>
/// 处理顺序：
/// 1) <c>[AllowAnonymous]</c> 短路（含 JwtBearerEvents.OnChallenge 已处理过的令牌无效场景）；
/// 2) <c>[SkipPermissionCheck]</c> 放行（白名单：只要登录即可访问）；
/// 3) 无 <c>[RequirePermission]</c> 标注 → 拒绝（默认拒绝：漏标被测试与运行时暴露，而非静默放开）；
/// 4) 标注的权限点全部命中才放行，缺失任意一点即拒绝。
/// </remarks>
public sealed class PermissionAuthorizationFilter : IAsyncAuthorizationFilter
{
    private readonly IPermissionResolver _permissionResolver;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<PermissionAuthorizationFilter> _logger;

    /// <summary>
    /// 初始化权限校验过滤器
    /// </summary>
    public PermissionAuthorizationFilter(
        IPermissionResolver permissionResolver,
        ICurrentUser currentUser,
        ILogger<PermissionAuthorizationFilter> logger)
    {
        _permissionResolver = permissionResolver;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (IsAnonymousAllowed(context))
        {
            return;
        }

        if (HasMetadata<SkipPermissionCheckAttribute>(context))
        {
            return;
        }

        var requiredPermissions = GetRequiredPermissions(context);
        if (requiredPermissions.Count == 0)
        {
            // 默认拒绝：业务动作必须显式登记权限点，漏标不容许静默放开
            _logger.LogWarning(
                "动作未标注权限点，按无权限拒绝：路径={Path}",
                context.HttpContext.Request.Path);
            context.Result = Forbidden();
            return;
        }

        var userId = _currentUser.UserId();
        if (userId is null)
        {
            // 理论上不可达：FallbackPolicy 已要求登录；此处兜底拒绝，避免空 Guid 被当作有效用户
            context.Result = Forbidden();
            return;
        }

        var granted = await _permissionResolver.GetPermissionsAsync(userId.Value, context.HttpContext.RequestAborted);

        var missing = requiredPermissions.Where(p => !granted.Contains(p)).ToList();
        if (missing.Count > 0)
        {
            _logger.LogWarning(
                "权限校验拒绝：用户={UserId}，路径={Path}，缺失权限点={MissingPermissions}",
                userId.Value,
                context.HttpContext.Request.Path,
                string.Join(",", missing));

            context.Result = Forbidden();
            return;
        }

        // 已授权：extra-scope 到此结束
    }

    private static bool IsAnonymousAllowed(AuthorizationFilterContext context)
    {
        var endpoint = context.HttpContext.GetEndpoint();
        var allowAnonymous = endpoint?.Metadata.GetMetadata<IAllowAnonymous>()
            ?? context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().FirstOrDefault();
        return allowAnonymous is not null;
    }

    private static bool HasMetadata<T>(AuthorizationFilterContext context)
        where T : class, IFilterMetadata
        => context.ActionDescriptor.FilterDescriptors.Any(x => x.Filter is T)
            || context.ActionDescriptor.EndpointMetadata.Any(x => x is T);

    private static List<string> GetRequiredPermissions(AuthorizationFilterContext context)
    {
        // 动作级与控制器级标注合并（「且」关系）：控制器级标注对全部动作生效
        return context.ActionDescriptor.EndpointMetadata.OfType<RequirePermissionAttribute>()
            .Select(x => x.PermissionKey)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static JsonResult Forbidden()
        => new(ApiResponseFactory.Fail(ErrorCode.Forbidden, "无权限操作")) { StatusCode = StatusCodes.Status200OK };
}
