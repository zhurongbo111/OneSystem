using App.Core;
using App.Core.Auth;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Auth.Login;
using App.Infrastructure;
using App.Infrastructure.Auth;
using App.Infrastructure.Persistence;
using App.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;

namespace App.Tests;

/// <summary>
/// 登录用例处理器测试（含"登录留痕"行为：成功写日志、失败不写）
/// </summary>
public class LoginRequestHandlerTests
{
    private static (LoginRequestHandler Handler, AppDbContext DbContext) CreateHandler()
    {
        var dbContext = TestSupport.CreateDbContext();
        TestSupport.SeedAdmin(dbContext);

        // 格式校验由 Mediator 分发前统一执行，处理器不再依赖校验器
        var handler = new LoginRequestHandler(
            new UserRepository(dbContext),
            new UserLoginLogRepository(dbContext),
            new UnitOfWork(dbContext),
            TestSupport.PasswordHasher,
            TestSupport.CreateTokenService(),
            new StubClientInfo { IpAddress = "127.0.0.1", UserAgent = "xunit-agent" },
            new PermissionResolver(dbContext));

        return (handler, dbContext);
    }

    [Fact]
    public async Task HandleAsync_正确账号_应返回token与用户()
    {
        var (handler, _) = CreateHandler();

        var result = await handler.HandleAsync(new LoginRequest { Username = "admin", Password = "admin123" });

        Assert.False(string.IsNullOrEmpty(result.Token));
        Assert.Equal("admin", result.User.Username);
        Assert.Equal("管理员", result.User.DisplayName);
        // 未绑定角色的用户权限集合为空，登录不报错
        Assert.Empty(result.Permissions);
    }

    [Fact]
    public async Task HandleAsync_用户绑定角色_应返回该角色的权限点()
    {
        var (handler, dbContext) = CreateHandler();
        var admin = await dbContext.Users.SingleAsync();
        var role = TestSupport.SeedRole(dbContext, "操作角色", false, Permissions.ProductsView, Permissions.PartnersView);
        TestSupport.BindRole(dbContext, admin, role);

        var result = await handler.HandleAsync(new LoginRequest { Username = "admin", Password = "admin123" });

        Assert.Equal(2, result.Permissions.Count);
        Assert.Contains(Permissions.ProductsView, result.Permissions);
        Assert.Contains(Permissions.PartnersView, result.Permissions);
    }

    [Fact]
    public async Task HandleAsync_超级管理员_应返回全量权限点()
    {
        var (handler, dbContext) = CreateHandler();
        var admin = await dbContext.Users.SingleAsync();
        var superAdmin = TestSupport.SeedRole(dbContext, BuiltinRoles.SuperAdmin, true);
        TestSupport.BindRole(dbContext, admin, superAdmin);

        var result = await handler.HandleAsync(new LoginRequest { Username = "admin", Password = "admin123" });

        // 超级管理员不逐点存储权限，解析时直接返回全量清单
        Assert.Equal(Permissions.All.Count, result.Permissions.Count);
    }

    [Fact]
    public async Task HandleAsync_错误密码_应抛业务异常40001()
    {
        var (handler, _) = CreateHandler();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new LoginRequest { Username = "admin", Password = "wrong" }));

        Assert.Equal(ErrorCode.LoginFailed, ex.Code);
    }

    [Fact]
    public async Task HandleAsync_用户不存在_应抛业务异常40001()
    {
        var (handler, _) = CreateHandler();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new LoginRequest { Username = "nobody", Password = "admin123" }));

        Assert.Equal(ErrorCode.LoginFailed, ex.Code);
    }

    [Fact]
    public async Task HandleAsync_账号已禁用_应抛业务异常40005()
    {
        var (_, dbContext) = CreateHandler();
        var blocked = TestSupport.NewUser("blocked", "被禁用", "user123", UserStatus.Disabled);
        dbContext.Users.Add(blocked);
        await dbContext.SaveChangesAsync();

        var handler = new LoginRequestHandler(
            new UserRepository(dbContext),
            new UserLoginLogRepository(dbContext),
            new UnitOfWork(dbContext),
            TestSupport.PasswordHasher,
            TestSupport.CreateTokenService(),
            new StubClientInfo(),
            new PermissionResolver(dbContext));

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new LoginRequest { Username = "blocked", Password = "user123" }));

        Assert.Equal(ErrorCode.UserDisabled, ex.Code);
    }

    [Fact]
    public async Task HandleAsync_正确账号_应写入登录日志并更新最近登录时间()
    {
        var (handler, dbContext) = CreateHandler();

        var result = await handler.HandleAsync(new LoginRequest { Username = "admin", Password = "admin123" });

        var log = await dbContext.UserLoginLogs.SingleAsync();
        Assert.Equal(Guid.Parse(result.User.Id), log.UserId);
        Assert.Equal("admin", log.Username);
        Assert.Equal("管理员", log.DisplayName);
        Assert.Equal("127.0.0.1", log.IpAddress);
        Assert.Equal("xunit-agent", log.UserAgent);
        Assert.True((DateTimeOffset.UtcNow - log.LoginAt).TotalMinutes < 1);

        var admin = await dbContext.Users.SingleAsync(u => u.Username == "admin");
        Assert.NotNull(admin.LastLoginAt);
    }

    [Fact]
    public async Task HandleAsync_密码错误_不应写入登录日志()
    {
        var (handler, dbContext) = CreateHandler();

        await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new LoginRequest { Username = "admin", Password = "wrong" }));

        Assert.Empty(await dbContext.UserLoginLogs.ToListAsync());
    }

    [Fact]
    public async Task HandleAsync_账号已禁用_不应写入登录日志()
    {
        var (_, dbContext) = CreateHandler();
        dbContext.Users.Add(TestSupport.NewUser("blocked", "被禁用", "user123", UserStatus.Disabled));
        await dbContext.SaveChangesAsync();

        var handler = new LoginRequestHandler(
            new UserRepository(dbContext),
            new UserLoginLogRepository(dbContext),
            new UnitOfWork(dbContext),
            TestSupport.PasswordHasher,
            TestSupport.CreateTokenService(),
            new StubClientInfo(),
            new PermissionResolver(dbContext));

        await Assert.ThrowsAsync<BusinessException>(
            () => handler.HandleAsync(new LoginRequest { Username = "blocked", Password = "user123" }));

        Assert.Empty(await dbContext.UserLoginLogs.ToListAsync());
    }
}
