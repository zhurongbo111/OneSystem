using App.Api.Authorization;
using App.Core.Auth;
using App.Core.Errors;
using App.Core.Features.Permissions.GetPermissions;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;

namespace App.Tests;

/// <summary>
/// 权限校验过滤器测试：命中 / 缺失 / 白名单 / 免标注四种分流，无权限统一 HTTP 200 + code 40300
/// </summary>
public class PermissionAuthorizationFilterTests
{
    private static Guid UserId { get; } = Guid.NewGuid();

    private static StubPermissionResolver Resolver(params string[] permissions) => new(permissions);

    private static AuthorizationFilterContext CreateContext(params object[] endpointMetadata)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(endpointMetadata),
            "test-endpoint"));

        // ActionDescriptor.EndpointMetadata 默认是定长数组，必须整体赋 List 才能写入
        var actionDescriptor = new ActionDescriptor { EndpointMetadata = endpointMetadata.ToList() };
        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            actionDescriptor,
            new ModelStateDictionary());

        return new AuthorizationFilterContext(actionContext, []);
    }

    private static PermissionAuthorizationFilter CreateFilter(params string[] grantedPermissions)
        => new(Resolver(grantedPermissions), new StubCurrentUser(UserId), NullLogger<PermissionAuthorizationFilter>.Instance);

    [Fact]
    public async Task OnAuthorizationAsync_权限命中_应放行()
    {
        var filter = CreateFilter(Permissions.PurchasesView, Permissions.PurchasesCreate);
        var context = CreateContext(new RequirePermissionAttribute(Permissions.PurchasesView));

        await filter.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public async Task OnAuthorizationAsync_权限缺失_应返回HTTP200且code40300()
    {
        var filter = CreateFilter(Permissions.PurchasesView);
        var context = CreateContext(new RequirePermissionAttribute(Permissions.PurchasesVoid));

        await filter.OnAuthorizationAsync(context);

        // 三期分流：无权限返回 HTTP 200 + code 40300（AGENTS.md §4.2），不是 403
        var jsonResult = Assert.IsType<JsonResult>(context.Result);
        Assert.Equal(StatusCodes.Status200OK, jsonResult.StatusCode);
        Assert.Equal(ErrorCode.Forbidden, (int)jsonResult.Value!.GetType().GetProperty("Code")!.GetValue(jsonResult.Value)!);
    }

    [Fact]
    public async Task OnAuthorizationAsync_多个权限点标注_缺任一个应拒绝()
    {
        var filter = CreateFilter(Permissions.PurchasesView);
        var context = CreateContext(
            new RequirePermissionAttribute(Permissions.PurchasesView),
            new RequirePermissionAttribute(Permissions.PurchasesVoid));

        await filter.OnAuthorizationAsync(context);

        Assert.NotNull(context.Result);
    }

    [Fact]
    public async Task OnAuthorizationAsync_多个权限点标注_全部命中应放行()
    {
        var filter = CreateFilter(Permissions.PurchasesView, Permissions.PurchasesVoid);
        var context = CreateContext(
            new RequirePermissionAttribute(Permissions.PurchasesView),
            new RequirePermissionAttribute(Permissions.PurchasesVoid));

        await filter.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public async Task OnAuthorizationAsync_免标注入口_应放行()
    {
        var filter = CreateFilter();
        var context = CreateContext(new SkipPermissionCheckAttribute());

        await filter.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public async Task OnAuthorizationAsync_匿名可访问_应放行且不查权限()
    {
        var resolver = Resolver(Permissions.PurchasesView);
        var filter = new PermissionAuthorizationFilter(
            resolver,
            new StubCurrentUser(UserId),
            NullLogger<PermissionAuthorizationFilter>.Instance);
        var context = CreateContext(new AllowAnonymousAttribute(), new RequirePermissionAttribute(Permissions.PurchasesVoid));

        await filter.OnAuthorizationAsync(context);

        Assert.Null(context.Result);
        Assert.Equal(0, resolver.CallCount);
    }

    [Fact]
    public async Task OnAuthorizationAsync_未标注权限点_应默认拒绝()
    {
        var filter = CreateFilter(Permissions.PurchasesView);
        var context = CreateContext();

        await filter.OnAuthorizationAsync(context);

        // 默认拒绝：未登记权限点的动作一律按无权限处理
        var jsonResult = Assert.IsType<JsonResult>(context.Result);
        Assert.Equal(ErrorCode.Forbidden, (int)jsonResult.Value!.GetType().GetProperty("Code")!.GetValue(jsonResult.Value)!);
    }

    [Fact]
    public async Task GetPermissions_权限点清单_应覆盖全部分组()
    {
        var groups = await new GetPermissionsRequestHandler()
            .HandleAsync(new GetPermissionsRequest());

        Assert.Equal(App.Core.Auth.Permissions.Groups.Count, groups.Count);
        Assert.All(groups, group =>
        {
            Assert.NotEqual(string.Empty, group.GroupName);
            Assert.NotEmpty(group.Items);
            Assert.All(group.Items, item => Assert.True(App.Core.Auth.Permissions.IsKnown(item.Key)));
        });
    }
}
