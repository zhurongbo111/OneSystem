using App.Api.Authorization;
using App.Api.Controllers;
using App.Core.Auth;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;

namespace App.Tests;

/// <summary>
/// 动作权限矩阵守卫测试：凡新增 Controller 动作，必须标注 <c>[RequirePermission]</c> 且权限点是
/// <c>App.Core.Auth.Permissions</c> 中已登记的 key（仍在白名单内的动作除外）。
/// 新增用例未标注 → 测试失败；白名单变更须同步更新此处清单。
/// </summary>
public class ApiPermissionMatrixTests
{
    private const string MeAction = "Me";
    private const string MyPermissionsAction = "MyPermissions";

    /// <summary>白名单：只要登录即可访问的动作（"Controller.动作"）</summary>
    private static readonly HashSet<string> Whitelist =
    [
        $"{nameof(UsersController)}.{MeAction}",
        $"{nameof(UsersController)}.{MyPermissionsAction}",
    ];

    [Fact]
    public void 所有非白名单动作_应标注合法权限点()
    {
        var missing = new List<string>();
        var invalidKeys = new List<string>();

        foreach (var method in EnumerateActions())
        {
            if (IsAllowAnonymous(method))
            {
                continue;
            }

            if (IsWhitelisted(method))
            {
                continue;
            }

            var keys = method.GetCustomAttributes(typeof(RequirePermissionAttribute), inherit: true)
                .Cast<RequirePermissionAttribute>()
                .Select(x => x.PermissionKey)
                .ToList();

            if (keys.Count == 0)
            {
                missing.Add($"{method.DeclaringType!.Name}.{method.Name}");
                continue;
            }

            invalidKeys.AddRange(
                keys.Where(key => !Permissions.IsKnown(key)).Select(key => $"{method.DeclaringType!.Name}.{method.Name} -> {key}"));
        }

        Assert.Empty(missing);
        Assert.Empty(invalidKeys);
    }

    [Fact]
    public void 白名单动作_不应标注权限点()
    {
        var annotated = EnumerateActions()
            .Where(IsWhitelisted)
            .Where(method => method.GetCustomAttributes(typeof(RequirePermissionAttribute), inherit: true).Length > 0)
            .Select(method => $"{method.DeclaringType!.Name}.{method.Name}")
            .ToList();

        Assert.Empty(annotated);
    }

    [Fact]
    public void 权限点清单_应覆盖已登记业务动作的标注()
    {
        var usedKeys = EnumerateActions()
            .SelectMany(method => method.GetCustomAttributes(typeof(RequirePermissionAttribute), inherit: true))
            .Cast<RequirePermissionAttribute>()
            .Select(x => x.PermissionKey)
            .Distinct(StringComparer.Ordinal);

        Assert.All(usedKeys, key => Assert.Contains(key, Permissions.All));
    }

    /// <summary>枚举所有 Controller 的动作方法（含 HTTP 方法标注的公开实例方法）</summary>
    private static IEnumerable<System.Reflection.MethodInfo> EnumerateActions()
        => typeof(RolesController).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && type.IsSubclassOf(typeof(Microsoft.AspNetCore.Mvc.ControllerBase)))
            .SelectMany(type => type.GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
            .Where(method => method.DeclaringType?.IsSubclassOf(typeof(Microsoft.AspNetCore.Mvc.ControllerBase)) == true)
            .Where(method => method.GetCustomAttributes(typeof(HttpMethodAttribute), inherit: true).Length > 0);

    private static bool IsAllowAnonymous(System.Reflection.MethodInfo method)
        => method.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).Length > 0
            || method.DeclaringType!.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true).Length > 0;

    private static bool IsWhitelisted(System.Reflection.MethodInfo method)
        => Whitelist.Contains($"{method.DeclaringType!.Name}.{method.Name}");
}
