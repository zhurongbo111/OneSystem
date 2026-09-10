using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Entities;
using App.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace App.Tests;

/// <summary>
/// Handler 单测公共支撑：独立的 InMemory 数据库、种子数据、无状态组件与桩依赖。
/// </summary>
internal static class TestSupport
{
    /// <summary>被测的密码哈希组件（无状态）</summary>
    public static readonly PasswordHasher PasswordHasher = new();

    /// <summary>创建一个独立的 InMemory AppDbContext（每个用例互不干扰）</summary>
    public static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"app-tests-{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>写入内置管理员（与 DatabaseInitializer 种子一致：admin/admin123）</summary>
    public static User SeedAdmin(AppDbContext dbContext, string password = "admin123")
    {
        var admin = NewUser("admin", "管理员", password);
        dbContext.Users.Add(admin);
        dbContext.SaveChanges();
        return admin;
    }

    /// <summary>构建用户实体（默认启用）</summary>
    public static User NewUser(
        string username = "user1",
        string displayName = "用户一",
        string password = "user123",
        UserStatus status = UserStatus.Enabled,
        string? email = null,
        string? phone = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            PasswordHash = PasswordHasher.Hash(password),
            DisplayName = displayName,
            Email = email,
            Phone = phone,
            Status = status,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>构建 TokenService（单测用固定密钥）</summary>
    public static TokenService CreateTokenService()
    {
        var options = new JwtOptions
        {
            Secret = new string('a', 48),
            Issuer = "app-api",
            Audience = "app-web",
            ExpiresMinutes = 120,
        };
        return new TokenService(Options.Create(options), NullLogger<TokenService>.Instance);
    }
}

/// <summary>当前登录用户桩</summary>
internal sealed class StubCurrentUser : ICurrentUser
{
    public StubCurrentUser(Guid id, string username = "admin", string displayName = "管理员")
    {
        Id = id.ToString();
        Username = username;
        DisplayName = displayName;
    }

    public string Id { get; }

    public string Username { get; }

    public string DisplayName { get; }
}

/// <summary>客户端信息桩</summary>
internal sealed class StubClientInfo : IClientInfo
{
    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }
}
