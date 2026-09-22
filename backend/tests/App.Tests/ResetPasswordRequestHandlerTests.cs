using App.Core.Errors;
using App.Core.Features.Users.ResetPassword;
using App.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;

namespace App.Tests;

/// <summary>
/// 重置密码用例处理器测试
/// </summary>
public class ResetPasswordRequestHandlerTests
{
    private static ResetPasswordRequestHandler CreateHandler(App.Infrastructure.AppDbContext dbContext, Guid operatorId)
        => new(new UserRepository(dbContext), TestSupport.PasswordHasher, new StubCurrentUser(operatorId), TestSupport.AuditLogger);

    [Fact]
    public async Task HandleAsync_重置后_新密码生效且旧密码失效()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var user = TestSupport.NewUser("alice", "张三", password: "old12345");
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var result = await CreateHandler(dbContext, Guid.NewGuid()).HandleAsync(
            new ResetPasswordRequest { Id = user.Id, NewPassword = "new12345" });

        Assert.Null(result);
        var saved = await dbContext.Users.SingleAsync();
        Assert.True(TestSupport.PasswordHasher.Verify("new12345", saved.PasswordHash));
        Assert.False(TestSupport.PasswordHasher.Verify("old12345", saved.PasswordHash));
    }

    [Fact]
    public async Task HandleAsync_用户不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => CreateHandler(dbContext, Guid.NewGuid()).HandleAsync(
                new ResetPasswordRequest { Id = Guid.NewGuid(), NewPassword = "new12345" }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }
}
