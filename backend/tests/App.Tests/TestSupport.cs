using App.Core.Abstractions;
using App.Core.Audit;
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

    /// <summary>审计日志桩：遗留用例不关心日志内容时的默认依赖（丢空）</summary>
    public static IAuditLogger AuditLogger { get; } = new RecordingAuditLogger();

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

    /// <summary>构建往来单位实体（默认启用 + 供应商类型）</summary>
    public static Partner NewPartner(
        string name = "供应商一",
        PartnerType type = PartnerType.Supplier,
        PartnerStatus status = PartnerStatus.Enabled)
    {
        var now = DateTimeOffset.UtcNow;
        return new Partner
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = type,
            Status = status,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>构建商品实体（默认启用）</summary>
    public static Product NewProduct(
        string code = "sku-test",
        string name = "商品一",
        decimal purchasePrice = 0m,
        ProductStatus status = ProductStatus.Enabled,
        Guid? categoryId = null,
        string unit = "个")
    {
        var now = DateTimeOffset.UtcNow;
        return new Product
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            CategoryId = categoryId ?? Guid.NewGuid(),
            Unit = unit,
            PurchasePrice = purchasePrice,
            SalePrice = purchasePrice,
            Status = status,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    /// <summary>构建角色实体（角色行与其权限点一并落库）</summary>
    public static Role SeedRole(
        AppDbContext dbContext,
        string name = "操作角色",
        bool isBuiltin = false,
        params string[] permissionKeys)
    {
        var now = DateTimeOffset.UtcNow;
        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = name,
            IsBuiltin = isBuiltin,
            CreatedAt = now,
            UpdatedAt = now,
        };
        dbContext.Roles.Add(role);
        dbContext.RolePermissions.AddRange(
            permissionKeys.Select(key => new RolePermission { RoleId = role.Id, PermissionKey = key }));
        dbContext.SaveChanges();
        return role;
    }

    /// <summary>将用户绑定到角色</summary>
    public static void BindRole(AppDbContext dbContext, User user, Role role)
    {
        dbContext.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        dbContext.SaveChanges();
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
        UserId = id;
        Id = id.ToString();
        Username = username;
        DisplayName = displayName;
    }

    /// <summary>原始 Guid 形式的用户 id（用于断言审计字段）</summary>
    public Guid UserId { get; }

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

/// <summary>
/// 审计日志桩：记录被测用例写入的每条 AuditEntry，供断言「写没写 / 写了什么」；
/// 默认不抛异常（事务与业务仍照常跑完）。
/// </summary>
internal sealed class RecordingAuditLogger : IAuditLogger
{
    /// <summary>按写入顺序记录的日志条目</summary>
    public List<AuditEntry> Entries { get; } = [];

    /// <summary>记录一条日志</summary>
    public Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        Entries.Add(entry);
        return Task.CompletedTask;
    }
}

/// <summary>权限解析器桩：返回固定权限点集合，并记录解析次数（校验是否被重复解析）</summary>
internal sealed class StubPermissionResolver : IPermissionResolver
{
    private readonly IReadOnlySet<string> _permissions;

    public StubPermissionResolver(params string[] permissions)
    {
        _permissions = new HashSet<string>(permissions, StringComparer.Ordinal);
    }

    /// <summary>解析调用次数（用于验证单请求内缓存行为）</summary>
    public int CallCount { get; private set; }

    public Task<IReadOnlySet<string>> GetPermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(_permissions);
    }
}
