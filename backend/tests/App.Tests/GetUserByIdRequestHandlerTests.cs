using App.Core.Errors;
using App.Core.Features.Users.GetUserById;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// 用户详情用例处理器测试
/// </summary>
public class GetUserByIdRequestHandlerTests
{
    [Fact]
    public async Task HandleAsync_用户存在_应返回详情()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var user = TestSupport.NewUser("alice", "张三", email: "alice@example.com", phone: "13800000001");
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var result = await new GetUserByIdRequestHandler(new UserRepository(dbContext))
            .HandleAsync(new GetUserByIdRequest { Id = user.Id });

        Assert.Equal(user.Id.ToString(), result.Id);
        Assert.Equal("alice", result.Username);
        Assert.Equal("张三", result.DisplayName);
        Assert.Equal("alice@example.com", result.Email);
        Assert.Equal("13800000001", result.Phone);
        Assert.Equal(1, result.Status);
    }

    [Fact]
    public async Task HandleAsync_用户不存在_应抛业务异常40400()
    {
        await using var dbContext = TestSupport.CreateDbContext();

        var ex = await Assert.ThrowsAsync<BusinessException>(
            () => new GetUserByIdRequestHandler(new UserRepository(dbContext))
                .HandleAsync(new GetUserByIdRequest { Id = Guid.NewGuid() }));

        Assert.Equal(ErrorCode.NotFound, ex.Code);
    }
}
