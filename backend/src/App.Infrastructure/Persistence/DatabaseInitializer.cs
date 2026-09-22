using App.Core;
using App.Core.Auth;
using App.Core.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.Persistence;

/// <summary>
/// 启动初始化：关系型数据库按环境执行迁移，并在用户表为空时创建内置管理员、随后幂等补齐内置角色与绑定。
/// 无注册入口，缺少引导管理员会导致系统被锁死，故用户种子对所有环境生效（"表空才建"，幂等）。
/// 角色与用户角色绑定种子同样幂等（存在即跳过、只补不删），见 specs/028-erp-rbac/design.md §2.4。
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

        if (!await dbContext.Users.AnyAsync(cancellationToken))
        {
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

        await SeedRolesAndUserRolesAsync(dbContext, logger, cancellationToken);
    }

    /// <summary>
    /// 幂等创建内置角色并补齐用户角色绑定：
    /// 1) 角色表为空 → 建 <c>SuperAdmin</c>（解析时全量放行，不逐点存储）与 <c>Staff</c>（全部业务权限，排除系统管理类）；
    /// 2) 内置管理员绑定 <c>SuperAdmin</c>（若尚未绑定）；
    /// 3) 无任何角色绑定的既有用户批量回填 <c>Staff</c>（升级后立即可用）。
    /// </summary>
    /// <param name="dbContext">数据库上下文</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="cancellationToken">取消令牌</param>
    private static async Task SeedRolesAndUserRolesAsync(
        AppDbContext dbContext,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Roles.AnyAsync(cancellationToken))
        {
            var now = DateTimeOffset.UtcNow;
            var superAdmin = new Role
            {
                Id = Guid.NewGuid(),
                Name = BuiltinRoles.SuperAdmin,
                Remark = "超级管理员，拥有全部权限点",
                IsBuiltin = true,
                CreatedAt = now,
                UpdatedAt = now,
            };
            var staff = new Role
            {
                Id = Guid.NewGuid(),
                Name = BuiltinRoles.Staff,
                Remark = "普通员工，默认角色（不含用户 / 角色 / 审计 / 成本重算管理动作）",
                IsBuiltin = true,
                CreatedAt = now,
                UpdatedAt = now,
            };

            dbContext.Roles.AddRange(superAdmin, staff);

            // SuperAdmin 不逐点存储权限（新增权限点自动继承）；Staff 存全部业务权限点
            dbContext.RolePermissions.AddRange(
                Permissions.All
                    .Except(Permissions.ExcludedFromStaff, StringComparer.Ordinal)
                    .Select(key => new RolePermission { RoleId = staff.Id, PermissionKey = key }));

            await dbContext.SaveChangesAsync(cancellationToken);
            logger?.LogInformation("角色表为空，已创建内置角色 {Roles}", string.Join(" / ", BuiltinRoles.SuperAdmin, BuiltinRoles.Staff));
        }

        var superAdminRole = await dbContext.Roles
            .FirstOrDefaultAsync(r => r.Name == BuiltinRoles.SuperAdmin, cancellationToken);
        var staffRole = await dbContext.Roles
            .FirstOrDefaultAsync(r => r.Name == BuiltinRoles.Staff, cancellationToken);
        if (superAdminRole is null || staffRole is null)
        {
            return;
        }

        var admin = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Username == DefaultAdminUsername, cancellationToken);
        if (admin is not null
            && !await dbContext.UserRoles.AnyAsync(ur => ur.UserId == admin.Id && ur.RoleId == superAdminRole.Id, cancellationToken))
        {
            dbContext.UserRoles.Add(new UserRole { UserId = admin.Id, RoleId = superAdminRole.Id });
            await dbContext.SaveChangesAsync(cancellationToken);
            logger?.LogInformation("内置管理员 {Username} 已绑定内置角色 {Role}", DefaultAdminUsername, BuiltinRoles.SuperAdmin);
        }

        // 幂等回填：只补不删，老用户升级后立即可用
        var userIdsWithoutRole = await dbContext.Users
            .Where(u => !dbContext.UserRoles.Any(ur => ur.UserId == u.Id))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        if (userIdsWithoutRole.Count > 0)
        {
            dbContext.UserRoles.AddRange(
                userIdsWithoutRole.Select(userId => new UserRole { UserId = userId, RoleId = staffRole.Id }));
            await dbContext.SaveChangesAsync(cancellationToken);
            logger?.LogInformation("已为 {Count} 个无角色用户回填默认角色 {Role}", userIdsWithoutRole.Count, BuiltinRoles.Staff);
        }
    }
}
