using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Users.UpdateUserStatus;
using App.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;

namespace App.Tests;

/// <summary>
/// 启用 / 禁用用户用例处理器测试
/// </summary>
public class UpdateUserStatusRequestHandlerTests
{
    private static UpdateUserStatusRequestHandler CreateHandler(App.Infrastructure.AppDbContext dbContext, Guid operatorId)
        => new(new UserRepository(dbContext), new UserRoleRepository(dbContext), new StubCurrentUser(operatorId), TestSupport.AuditLogger);

    [Fact]
    public async Task HandleAsync_禁用其他用户_应更新为禁用()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var target = TestSupport.NewUser("alice", "张三");
        var operatorId = Guid.NewGuid();
        dbContext.Users.AddRange(target, TestSupport.NewUser("admin", "管理员"));
        await dbContext.SaveChangesAsync();

        var result = await CreateHandler(dbContext, operatorId).HandleAsync(
            new UpdateUserStatusRequest { Id = target.Id, Status = 0 });

        Assert.Equal(0, result.Status);
        var saved = await dbContext.Users.SingleAsync(u => u.Id == target.Id);
        Assert.Equal(UserStatus.Disabled, saved.Status);
    }

    [Fact]
    public async Task HandleAsync_启用已禁用用户_应更新为启用()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var target = TestSupport.NewUser("alice", "张三", status: UserStatus.Disabled);
        dbContext.Users.Add(target);
        await dbContext.SaveChangesAsync();

        var result = await CreateHandler(dbContext, Guid.NewGuid()).HandleAsync(
            new UpdateUserStatusRequest { Id = target.Id, Status = 1 });

        Assert.Equal(1, result.Status);
    }

    [Fact]
    public async Task HandleAsync_禁用当前登录账号_应抛业务异常40006()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var admin = TestSupport.NewUser("admin", "管理员");
        dbContext.Users.Add(admin);
        await dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateHandler(dbContext, admin.Id).HandleAsync(
                new UpdateUserStatusRequest { Id = admin.Id, Status = 0 }));

        Assert.Equal(ErrorCode.CannotDisableSelf, ex.Code);
    }

    [Fact]
    public async Task HandleAsync_用户不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateHandler(dbContext, Guid.NewGuid()).HandleAsync(
                new UpdateUserStatusRequest { Id = Guid.NewGuid(), Status = 0 }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }
}
