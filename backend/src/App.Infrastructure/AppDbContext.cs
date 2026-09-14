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

    /// <summary>商品分类表</summary>
    public DbSet<Category> Categories => Set<Category>();

    /// <summary>商品表</summary>
    public DbSet<Product> Products => Set<Product>();

    /// <summary>库存台账表（与商品 1:1）</summary>
    public DbSet<Inventory> Inventory => Set<Inventory>();

    /// <summary>往来单位表（供应商 / 客户合并）</summary>
    public DbSet<Partner> Partners => Set<Partner>();

    /// <summary>采购单表</summary>
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();

    /// <summary>采购单明细表</summary>
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
