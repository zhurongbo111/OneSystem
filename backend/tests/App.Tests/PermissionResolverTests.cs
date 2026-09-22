using App.Core;
using App.Core.Auth;
using App.Infrastructure.Auth;

using Microsoft.EntityFrameworkCore;

namespace App.Tests;

/// <summary>
/// 权限解析器测试（用户 → 角色 → 权限点）
/// </summary>
public class PermissionResolverTests
{
    [Fact]
    public async Task GetPermissionsAsync_无角色用户_应返回空集合()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var user = TestSupport.NewUser("alice", "张三");
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var permissions = await new PermissionResolver(dbContext).GetPermissionsAsync(user.Id);

        Assert.Empty(permissions);
    }

    [Fact]
    public async Task GetPermissionsAsync_多角色_应返回权限点并集()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var user = TestSupport.NewUser("alice", "张三");
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        TestSupport.BindRole(dbContext, user, TestSupport.SeedRole(dbContext, "角色甲", false, Permissions.PurchasesView, Permissions.PurchasesCreate));
        TestSupport.BindRole(dbContext, user, TestSupport.SeedRole(dbContext, "角色乙", false, Permissions.PurchasesView, Permissions.PurchasesVoid));

        var permissions = await new PermissionResolver(dbContext).GetPermissionsAsync(user.Id);

        Assert.Equal(3, permissions.Count);
        Assert.Contains(Permissions.PurchasesView, permissions);
        Assert.Contains(Permissions.PurchasesCreate, permissions);
        Assert.Contains(Permissions.PurchasesVoid, permissions);
    }

    [Fact]
    public async Task GetPermissionsAsync_内置超级管理员_应返回全量权限点()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var user = TestSupport.NewUser("admin", "管理员");
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        // 超级管理员不逐点存储权限，命中即全量放行
        TestSupport.BindRole(dbContext, user, TestSupport.SeedRole(dbContext, BuiltinRoles.SuperAdmin, true));

        var permissions = await new PermissionResolver(dbContext).GetPermissionsAsync(user.Id);

        Assert.Equal(Permissions.All.Count, permissions.Count);
        Assert.Contains(Permissions.RolesDelete, permissions);
    }

    [Fact]
    public async Task GetPermissionsAsync_同一实例重复调用_应只查库一次()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var user = TestSupport.NewUser("alice", "张三");
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        TestSupport.BindRole(dbContext, user, TestSupport.SeedRole(dbContext, "角色甲", false, Permissions.PurchasesView));
        var resolver = new PermissionResolver(dbContext);

        var first = await resolver.GetPermissionsAsync(user.Id);
        // 缓存后即便库中数据变化，同一请求内（同一解析器实例）仍返回首次结果
        TestSupport.BindRole(dbContext, user, TestSupport.SeedRole(dbContext, "角色乙", false, Permissions.PurchasesVoid));
        var second = await resolver.GetPermissionsAsync(user.Id);

        Assert.Single(first);
        Assert.Single(second);
    }

    [Fact]
    public async Task GetPermissionsAsync_角色被清空_下次请求应立即可见()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var user = TestSupport.NewUser("alice", "张三");
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        var role = TestSupport.SeedRole(dbContext, "角色甲", false, Permissions.PurchasesView);
        TestSupport.BindRole(dbContext, user, role);
        Assert.Single(await new PermissionResolver(dbContext).GetPermissionsAsync(user.Id));

        dbContext.UserRoles.RemoveRange(await dbContext.UserRoles.ToListAsync());
        await dbContext.SaveChangesAsync();

        Assert.Empty(await new PermissionResolver(dbContext).GetPermissionsAsync(user.Id));
    }
}
