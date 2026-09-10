using App.Core.Entities;
using App.Core.Features.LoginLogs.GetLoginLogs;
using App.Infrastructure.Repositories;

namespace App.Tests;

/// <summary>
/// 登录日志查询用例处理器测试
/// </summary>
public class GetLoginLogsRequestHandlerTests
{
    private static GetLoginLogsRequestHandler CreateHandler(App.Infrastructure.AppDbContext dbContext)
        => new(new UserLoginLogRepository(dbContext));

    private static UserLoginLog NewLog(Guid userId, string username, DateTimeOffset loginAt, string? ipAddress = null)
        => new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Username = username,
            DisplayName = username,
            LoginAt = loginAt,
            IpAddress = ipAddress,
            UserAgent = "xunit-agent",
        };

    [Fact]
    public async Task HandleAsync_无筛选_应按登录时间倒序返回全部()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var userId = Guid.NewGuid();
        var baseTime = new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);
        dbContext.UserLoginLogs.AddRange(
            NewLog(userId, "admin", baseTime),
            NewLog(userId, "admin", baseTime.AddHours(2)),
            NewLog(userId, "alice", baseTime.AddHours(1)));
        await dbContext.SaveChangesAsync();

        var result = await CreateHandler(dbContext).HandleAsync(new GetLoginLogsRequest());

        Assert.Equal(3, result.Total);
        Assert.Equal(baseTime.AddHours(2), result.Items[0].LoginAt);
        Assert.Equal(baseTime.AddHours(1), result.Items[1].LoginAt);
        Assert.Equal(baseTime, result.Items[2].LoginAt);
    }

    [Fact]
    public async Task HandleAsync_按登录名模糊筛选_应忽略大小写()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var userId = Guid.NewGuid();
        var baseTime = new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);
        dbContext.UserLoginLogs.AddRange(
            NewLog(userId, "admin", baseTime),
            NewLog(userId, "alice", baseTime.AddHours(1)));
        await dbContext.SaveChangesAsync();

        var result = await CreateHandler(dbContext).HandleAsync(new GetLoginLogsRequest { Username = "ADM" });

        Assert.Equal(1, result.Total);
        Assert.Equal("admin", result.Items[0].Username);
    }

    [Fact]
    public async Task HandleAsync_按时间范围筛选_应为闭区间()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var userId = Guid.NewGuid();
        var baseTime = new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);
        dbContext.UserLoginLogs.AddRange(
            NewLog(userId, "admin", baseTime.AddHours(-1)),
            NewLog(userId, "admin", baseTime),
            NewLog(userId, "admin", baseTime.AddHours(1)));
        await dbContext.SaveChangesAsync();

        var result = await CreateHandler(dbContext).HandleAsync(new GetLoginLogsRequest
        {
            StartTime = baseTime,
            EndTime = baseTime.AddHours(1),
        });

        Assert.Equal(2, result.Total);
    }

    [Fact]
    public async Task HandleAsync_无匹配数据_应返回空列表()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        dbContext.UserLoginLogs.Add(NewLog(Guid.NewGuid(), "admin", DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();

        var result = await CreateHandler(dbContext).HandleAsync(new GetLoginLogsRequest { Username = "nobody" });

        Assert.Equal(0, result.Total);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task HandleAsync_分页_应返回对应页数据()
    {
        await using var dbContext = TestSupport.CreateDbContext();
        var userId = Guid.NewGuid();
        var baseTime = new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < 5; i++)
        {
            dbContext.UserLoginLogs.Add(NewLog(userId, "admin", baseTime.AddMinutes(i)));
        }

        await dbContext.SaveChangesAsync();

        var result = await CreateHandler(dbContext).HandleAsync(new GetLoginLogsRequest { Page = 2, PageSize = 2 });

        Assert.Equal(5, result.Total);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, result.Page);
    }
}
