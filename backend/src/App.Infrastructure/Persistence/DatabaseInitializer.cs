using App.Core.Auth;
using App.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.Persistence;

/// <summary>
/// 启动初始化：关系型数据库按环境执行迁移，并在用户表为空时创建内置管理员。
/// 无注册入口，缺少引导管理员会导致系统被锁死，故种子对所有环境生效（"表空才建"，幂等）。
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>内置管理员登录名</summary>
    public const string DefaultAdminUsername = "admin";

    /// <summary>内置管理员初始密码</summary>
    public const string DefaultAdminPassword = "admin123";

    /// <summary>
    /// 执行数据库初始化（在 Program 中于 app.Run() 之前调用一次）
    /// </summary>
    /// <param name="services">应用根服务提供器（内部自建 scope）</param>
    /// <param name="applyMigrations">是否执行迁移（仅 Development 为 true，生产由发布流程显式执行）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task InitializeAsync(
        IServiceProvider services,
        bool applyMigrations,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<PasswordHasher>();
        var logger = scope.ServiceProvider.GetService<ILoggerFactory>()
            ?.CreateLogger(typeof(DatabaseInitializer).FullName!);

        // InMemory（集成测试）等非关系型提供程序不支持迁移，跳过
        if (applyMigrations && dbContext.Database.IsRelational())
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }

        if (await dbContext.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        dbContext.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Username = DefaultAdminUsername,
            DisplayName = "管理员",
            PasswordHash = passwordHasher.Hash(DefaultAdminPassword),
            Status = UserStatus.Enabled,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = null,
            UpdatedBy = null,
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        logger?.LogInformation("用户表为空，已创建内置管理员账号 {Username}", DefaultAdminUsername);
    }
}
