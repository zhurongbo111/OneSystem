using App.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure;

/// <summary>
/// 应用 DbContext（PostgreSQL / Npgsql）。
/// 表结构通过 EF Core Migrations 管理；实体配置集中在 Persistence/Configurations 下。
/// </summary>
public class AppDbContext : DbContext
{
    /// <summary>
    /// 初始化 DbContext
    /// </summary>
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    /// <summary>用户表</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>用户登录日志表</summary>
    public DbSet<UserLoginLog> UserLoginLogs => Set<UserLoginLog>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
