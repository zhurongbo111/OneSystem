using App.Core;
using App.Core.Auth;
using App.Core.Entities;
using App.Core.Finance;

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
        await SeedPresetAccountsAsync(dbContext, logger, cancellationToken);
        await SeedAccountingPeriodsAsync(dbContext, logger, cancellationToken);
        await SeedAccountMappingsAsync(dbContext, logger, cancellationToken);
    }

    /// <summary>
    /// 幂等预置会计期间：补齐**当年** 1–12 月（<c>Open</c>），只补不删，可重复执行
    /// （specs/033-erp-general-ledger/design.md §2.6）。
    /// </summary>
    /// <param name="dbContext">数据库上下文</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="cancellationToken">取消令牌</param>
    private static async Task SeedAccountingPeriodsAsync(
        AppDbContext dbContext,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        var year = DateTimeOffset.UtcNow.Year;
        var existingMonths = await dbContext.AccountingPeriods
            .Where(p => p.Year == year)
            .Select(p => p.Month)
            .ToListAsync(cancellationToken);
        var existing = new HashSet<int>(existingMonths);

        var missing = Enumerable.Range(1, 12).Where(month => !existing.Contains(month)).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        dbContext.AccountingPeriods.AddRange(missing.Select(month => new AccountingPeriod
        {
            Id = Guid.NewGuid(),
            Year = year,
            Month = month,
            Status = PeriodStatus.Open,
        }));

        await dbContext.SaveChangesAsync(cancellationToken);
        logger?.LogInformation("已预置 {Year} 年会计期间 {Count} 个（已存在 {Skipped} 个跳过）", year, missing.Count, 12 - missing.Count);
    }

    /// <summary>
    /// 幂等预置科目映射（<see cref="AccountMappingKeys.All"/> 8 个键 → `031` 预置科目，按编码查）：
    /// 只补不删，可重复执行；预置科目被删除时该键跳过（缺失映射会让自动凭证拒绝生成 40158）
    /// （specs/033-erp-general-ledger/design.md §2.6）。
    /// </summary>
    /// <param name="dbContext">数据库上下文</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="cancellationToken">取消令牌</param>
    private static async Task SeedAccountMappingsAsync(
        AppDbContext dbContext,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        var existingKeys = await dbContext.AccountMappings
            .Select(m => m.Key)
            .ToListAsync(cancellationToken);
        var existing = new HashSet<string>(existingKeys, StringComparer.Ordinal);

        var definitions = AccountMappingKeys.All.Where(d => !existing.Contains(d.Key)).ToList();
        if (definitions.Count == 0)
        {
            return;
        }

        var codes = definitions.Select(d => d.PresetAccountCode).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var accounts = await dbContext.Accounts
            .Where(a => codes.Contains(a.Code))
            .Select(a => new { a.Id, a.Code })
            .ToListAsync(cancellationToken);
        var accountIdsByCode = accounts.ToDictionary(a => a.Code, a => a.Id, StringComparer.OrdinalIgnoreCase);

        var now = DateTimeOffset.UtcNow;
        var mappings = definitions
            .Where(d => accountIdsByCode.ContainsKey(d.PresetAccountCode))
            .Select(d => new AccountMapping
            {
                Id = Guid.NewGuid(),
                Key = d.Key,
                AccountId = accountIdsByCode[d.PresetAccountCode],
                CreatedAt = now,
                UpdatedAt = now,
            })
            .ToList();

        if (mappings.Count == 0)
        {
            return;
        }

        dbContext.AccountMappings.AddRange(mappings);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger?.LogInformation("已预置科目映射 {Count} 条（跳过 {Skipped} 条）", mappings.Count, definitions.Count - mappings.Count);
    }

    /// <summary>
    /// 幂等预置标准会计科目（<c>IsPreset = true</c>，预置科目不可删除、可改名 / 停用）：
    /// 按 <c>Code</c> 判定是否已存在，只补不删，可重复执行
    /// （specs/031-erp-finance-master/design.md §2.4）。
    /// </summary>
    /// <param name="dbContext">数据库上下文</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="cancellationToken">取消令牌</param>
    private static async Task SeedPresetAccountsAsync(
        AppDbContext dbContext,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        var existingCodes = await dbContext.Accounts
            .Select(a => a.Code)
            .ToListAsync(cancellationToken);
        var existing = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);

        var all = PresetAccounts();
        var presets = all
            .Select((preset, index) => (Preset: preset, SortOrder: index + 1))
            .Where(item => !existing.Contains(item.Preset.Code))
            .ToList();

        if (presets.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        dbContext.Accounts.AddRange(presets.Select(item => new Account
        {
            Id = Guid.NewGuid(),
            Code = item.Preset.Code,
            Name = item.Preset.Name,
            Category = item.Preset.Category,
            Direction = item.Preset.Direction,
            ParentId = null,
            SortOrder = item.SortOrder,
            IsPreset = true,
            Status = AccountStatus.Enabled,
            CreatedAt = now,
            UpdatedAt = now,
        }));

        await dbContext.SaveChangesAsync(cancellationToken);
        logger?.LogInformation("已预置标准会计科目 {Count} 条（已存在 {Skipped} 条跳过）", presets.Count, all.Count - presets.Count);
    }

    /// <summary>
    /// 最小标准科目表（顺序即一级科目排序）：
    /// 资产 / 成本默认借方，负债 / 权益 / 损益默认贷方（specs/031-erp-finance-master/design.md §2.4）
    /// </summary>
    private static IReadOnlyList<(string Code, string Name, AccountCategory Category, AccountDirection Direction)> PresetAccounts()
        =>
        [
            ("1001", "库存现金", AccountCategory.Asset, AccountDirection.Debit),
            ("1002", "银行存款", AccountCategory.Asset, AccountDirection.Debit),
            ("1122", "应收账款", AccountCategory.Asset, AccountDirection.Debit),
            ("1405", "库存商品", AccountCategory.Asset, AccountDirection.Debit),
            ("1403", "原材料", AccountCategory.Asset, AccountDirection.Debit),
            ("2202", "应付账款", AccountCategory.Liability, AccountDirection.Credit),
            ("2221", "应交税费", AccountCategory.Liability, AccountDirection.Credit),
            ("4001", "实收资本", AccountCategory.Equity, AccountDirection.Credit),
            ("4103", "本年利润", AccountCategory.Equity, AccountDirection.Credit),
            ("4104", "利润分配", AccountCategory.Equity, AccountDirection.Credit),
            ("5001", "生产成本", AccountCategory.Cost, AccountDirection.Debit),
            ("6001", "主营业务收入", AccountCategory.ProfitLoss, AccountDirection.Credit),
            ("6401", "主营业务成本", AccountCategory.ProfitLoss, AccountDirection.Credit),
            ("6602", "管理费用", AccountCategory.ProfitLoss, AccountDirection.Credit),
            ("6603", "财务费用", AccountCategory.ProfitLoss, AccountDirection.Credit),
        ];

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
