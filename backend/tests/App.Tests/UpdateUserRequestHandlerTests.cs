using App.Core.Errors;
using App.Core.Features.Users.UpdateUser;
using App.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace App.Tests;

/// <summary>
/// 编辑用户用例处理器测试
/// </summary>
public class UpdateUserRequestHandlerTests
{
    private static UpdateUserRequestHandler CreateHandler(App.Infrastructure.AppDbContext dbContext, Guid operatorId)
        => new(new UserRepository(dbContext), new StubCurrentUser(operatorId));

    [Fact]
    public async Task HandleAsync_合法请求_应更新展示字段与审计字段()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var user = TestSupport.NewUser("alice", "旧名称");
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        var operatorId = Guid.NewGuid();

        var result = await CreateHandler(dbContext, operatorId).HandleAsync(new UpdateUserRequest
        {
            Id = user.Id,
            DisplayName = "新名称",
            Email = "new@example.com",
            Phone = "13900000002",
        });

        var saved = await dbContext.Users.SingleAsync();
        Assert.Equal("alice", saved.Username);
        Assert.Equal("新名称", saved.DisplayName);
        Assert.Equal("new@example.com", saved.Email);
        Assert.Equal("13900000002", saved.Phone);
        Assert.Equal(operatorId, saved.UpdatedBy);
        Assert.Equal("新名称", result.DisplayName);
    }

    [Fact]
    public async Task HandleAsync_用户不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateHandler(dbContext, Guid.NewGuid()).HandleAsync(new UpdateUserRequest
            {
                Id = Guid.NewGuid(),
                DisplayName = "任意",
            }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }

    [Fact]
    public async Task HandleAsync_邮箱被他人占用_应抛业务异常40003()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var alice = TestSupport.NewUser("alice", "张三");
        var bob = TestSupport.NewUser("bob", "李四", email: "bob@example.com");
        dbContext.Users.AddRange(alice, bob);
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateHandler(dbContext, Guid.NewGuid()).HandleAsync(new UpdateUserRequest
            {
                Id = alice.Id,
                DisplayName = "张三",
                Email = "bob@example.com",
            }));

        Assert.Equal(ErrorCode.EmailExists, ex.Code);
    }

    [Fact]
    public async Task HandleAsync_邮箱为自身已有值_应允许保存()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var alice = TestSupport.NewUser("alice", "张三", email: "alice@example.com");
        dbContext.Users.Add(alice);
        await dbContext.SaveChangesAsync();

        var result = await CreateHandler(dbContext, Guid.NewGuid()).HandleAsync(new UpdateUserRequest
        {
            Id = alice.Id,
            DisplayName = "张三丰",
            Email = "alice@example.com",
        });

        Assert.Equal("张三丰", result.DisplayName);
        Assert.Equal("alice@example.com", result.Email);
    }

    [Fact]
    public async Task HandleAsync_手机号被他人占用_应抛业务异常40004()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var alice = TestSupport.NewUser("alice", "张三");
        var bob = TestSupport.NewUser("bob", "李四", phone: "13800000001");
        dbContext.Users.AddRange(alice, bob);
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateHandler(dbContext, Guid.NewGuid()).HandleAsync(new UpdateUserRequest
            {
                Id = alice.Id,
                DisplayName = "张三",
                Phone = "13800000001",
            }));

        Assert.Equal(ErrorCode.PhoneExists, ex.Code);
    }
}
