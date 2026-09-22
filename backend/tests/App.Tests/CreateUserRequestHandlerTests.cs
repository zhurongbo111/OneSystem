using App.Core.Auth;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Users.CreateUser;
using App.Infrastructure.Persistence;
using App.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;

namespace App.Tests;

/// <summary>
/// 新增用户用例处理器测试
/// </summary>
public class CreateUserRequestHandlerTests
{
    private static CreateUserRequestHandler CreateHandler(App.Infrastructure.AppDbContext dbContext, Guid operatorId)
        => new(
            new UserRepository(dbContext),
            new RoleRepository(dbContext),
            new UserRoleRepository(dbContext),
            new UnitOfWork(dbContext),
            TestSupport.PasswordHasher,
            new StubCurrentUser(operatorId),
            TestSupport.AuditLogger);

    private static CreateUserRequest NewUserRequest(
        string username = "alice",
        string email = "alice@example.com",
        string? phone = "13800000001",
        IReadOnlyList<Guid>? roleIds = null)
        => new()
        {
            Username = username,
            DisplayName = "张三",
            Email = email,
            Phone = phone,
            Password = "alice123",
            RoleIds = roleIds ?? [],
        };

    [Fact]
    public async Task HandleAsync_合法请求_应创建启用用户并哈希密码()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var operatorId = Guid.NewGuid();

        var result = await CreateHandler(dbContext, operatorId).HandleAsync(new CreateUserRequest
        {
            Username = "alice",
            DisplayName = "张三",
            Email = "Alice@Example.com",
            Phone = "13800000001",
            Password = "alice123",
        });

        var saved = await dbContext.Users.SingleAsync();
        Assert.Equal("alice", saved.Username);
        Assert.Equal(UserStatus.Enabled, saved.Status);
        Assert.Equal("alice@example.com", saved.Email);
        Assert.DoesNotContain("alice123", saved.PasswordHash);
        Assert.True(TestSupport.PasswordHasher.Verify("alice123", saved.PasswordHash));
        Assert.Equal(operatorId, saved.CreatedBy);
        Assert.Equal(operatorId, saved.UpdatedBy);
        Assert.Equal(saved.Id.ToString(), result.Id);
        Assert.Equal(1, result.Status);
    }

    [Fact]
    public async Task HandleAsync_可选字段为空串_应按未填写处理()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        await CreateHandler(dbContext, Guid.NewGuid()).HandleAsync(new CreateUserRequest
        {
            Username = "alice",
            DisplayName = "张三",
            Email = "   ",
            Phone = null,
            Password = "alice123",
        });

        var saved = await dbContext.Users.SingleAsync();
        Assert.Null(saved.Email);
        Assert.Null(saved.Phone);
    }

    [Fact]
    public async Task HandleAsync_用户名重复_应抛业务异常40002()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        dbContext.Users.Add(TestSupport.NewUser("alice", "已有用户"));
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateHandler(dbContext, Guid.NewGuid()).HandleAsync(new CreateUserRequest
            {
                Username = "ALICE",
                DisplayName = "张三",
                Password = "alice123",
            }));

        Assert.Equal(ErrorCode.UsernameExists, ex.Code);
    }

    [Fact]
    public async Task HandleAsync_邮箱重复_应抛业务异常40003()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        dbContext.Users.Add(TestSupport.NewUser("bob", "已有用户", email: "taken@example.com"));
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateHandler(dbContext, Guid.NewGuid()).HandleAsync(new CreateUserRequest
            {
                Username = "alice",
                DisplayName = "张三",
                Email = "TAKEN@example.com",
                Password = "alice123",
            }));

        Assert.Equal(ErrorCode.EmailExists, ex.Code);
    }

    [Fact]
    public async Task HandleAsync_手机号重复_应抛业务异常40004()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        dbContext.Users.Add(TestSupport.NewUser("bob", "已有用户", phone: "13800000001"));
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateHandler(dbContext, Guid.NewGuid()).HandleAsync(new CreateUserRequest
            {
                Username = "alice",
                DisplayName = "张三",
                Phone = "13800000001",
                Password = "alice123",
            }));

        Assert.Equal(ErrorCode.PhoneExists, ex.Code);
    }

    [Fact]
    public async Task HandleAsync_传入角色_应写入用户角色绑定并在详情返回()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var role = TestSupport.SeedRole(dbContext, "操作角色", false, Permissions.ProductsView);

        var result = await CreateHandler(dbContext, Guid.NewGuid()).HandleAsync(NewUserRequest(roleIds: [role.Id]));

        var userId = Guid.Parse(result.Id);
        Assert.Equal([role.Id], await dbContext.UserRoles.Where(ur => ur.UserId == userId).Select(ur => ur.RoleId).ToListAsync());
        Assert.Single(result.Roles);
        Assert.Equal(role.Id.ToString(), result.Roles[0].Id);
        Assert.Equal("操作角色", result.Roles[0].Name);
    }

    [Fact]
    public async Task HandleAsync_角色不存在_应抛业务异常40400且不落用户()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        TestSupport.SeedRole(dbContext, "操作角色");

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateHandler(dbContext, Guid.NewGuid()).HandleAsync(NewUserRequest(roleIds: [Guid.NewGuid()])));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
        Assert.Empty(await dbContext.Users.ToListAsync());
        Assert.Empty(await dbContext.UserRoles.ToListAsync());
    }

    [Fact]
    public async Task HandleAsync_角色id重复_应去重后只绑定一次()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var role = TestSupport.SeedRole(dbContext, "操作角色");

        var result = await CreateHandler(dbContext, Guid.NewGuid()).HandleAsync(NewUserRequest(roleIds: [role.Id, role.Id]));

        Assert.Single(await dbContext.UserRoles.ToListAsync());
        Assert.Single(result.Roles);
    }
}
