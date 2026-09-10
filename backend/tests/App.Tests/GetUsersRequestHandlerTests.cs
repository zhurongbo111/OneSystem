using App.Core.Entities;
using App.Core.Features.Users.GetUsers;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// 用户分页列表用例处理器测试
/// </summary>
public class GetUsersRequestHandlerTests
{
    private static GetUsersRequestHandler CreateHandler(App.Infrastructure.AppDbContext dbContext)
        => new(new UserRepository(dbContext));

    [Fact]
    public async Task HandleAsync_无筛选_应返回全部并按创建时间倒序()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var older = TestSupport.NewUser("older", "较早");
        older.CreatedAt = DateTimeOffset.UtcNow.AddDays(-1);
        var newer = TestSupport.NewUser("newer", "较新");
        dbContext.Users.AddRange(older, newer);
        await dbContext.SaveChangesAsync();

        var result = await CreateHandler(dbContext).HandleAsync(new GetUsersRequest());

        Assert.Equal(2, result.Total);
        Assert.Equal("newer", result.Items[0].Username);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
    }

    [Fact]
    public async Task HandleAsync_关键词_应同时匹配用户名与显示名()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        dbContext.Users.AddRange(
            TestSupport.NewUser("alice", "张三"),
            TestSupport.NewUser("bob", "李四"),
            TestSupport.NewUser("carol", "Alice 同名"));
        await dbContext.SaveChangesAsync();

        var byUsername = await CreateHandler(dbContext).HandleAsync(new GetUsersRequest { Keyword = "ALI" });
        Assert.Equal(2, byUsername.Total);
        Assert.Contains(byUsername.Items, x => x.Username == "alice");
        Assert.Contains(byUsername.Items, x => x.Username == "carol");

        var byDisplayName = await CreateHandler(dbContext).HandleAsync(new GetUsersRequest { Keyword = "李四" });
        Assert.Equal(1, byDisplayName.Total);
        Assert.Equal("bob", byDisplayName.Items[0].Username);
    }

    [Fact]
    public async Task HandleAsync_状态筛选_应只返回对应状态()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        dbContext.Users.AddRange(
            TestSupport.NewUser("enabled1", "启用一"),
            TestSupport.NewUser("disabled1", "禁用一", status: UserStatus.Disabled));
        await dbContext.SaveChangesAsync();

        var disabled = await CreateHandler(dbContext).HandleAsync(new GetUsersRequest { Status = 0 });
        Assert.Equal(1, disabled.Total);
        Assert.Equal("disabled1", disabled.Items[0].Username);
        Assert.Equal(0, disabled.Items[0].Status);

        var enabled = await CreateHandler(dbContext).HandleAsync(new GetUsersRequest { Status = 1 });
        Assert.Equal(1, enabled.Total);
        Assert.Equal("enabled1", enabled.Items[0].Username);
    }

    [Fact]
    public async Task HandleAsync_分页_应返回对应页数据()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        for (var i = 0; i < 5; i++)
        {
            var user = TestSupport.NewUser($"user{i}", $"用户{i}");
            user.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(i);
            dbContext.Users.Add(user);
        }

        await dbContext.SaveChangesAsync();

        var result = await CreateHandler(dbContext).HandleAsync(new GetUsersRequest { Page = 2, PageSize = 2 });

        Assert.Equal(5, result.Total);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
    }
}
