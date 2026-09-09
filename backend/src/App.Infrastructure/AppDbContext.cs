using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure;

/// <summary>
/// 应用 DbContext（PostgreSQL / Npgsql）。
/// 脚手架阶段暂无实体；首个业务功能建表时在此配置 DbSet 并走 EF Core Migrations。
/// </summary>
public class AppDbContext : DbContext
{
    /// <summary>
    /// 初始化 DbContext
    /// </summary>
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
}
