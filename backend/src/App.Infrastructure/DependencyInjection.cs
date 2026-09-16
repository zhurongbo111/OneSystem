using App.Core.Abstractions;
using App.Infrastructure.Persistence;
using App.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace App.Infrastructure;

/// <summary>
/// App.Infrastructure 服务注册扩展
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 注册 EF Core（PostgreSQL / Npgsql）、IUnitOfWork 与仓储实现。
    /// 连接串为敏感配置，只从环境变量 ConnectionStrings__Default 读取（AGENTS.md §7），
    /// 未配置时退化为不含凭据的本地占位串以允许启动，首次查库会因缺少凭据报错。
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            // 连接串（含账号口令）禁止入库 / 硬编码，只从环境变量 ConnectionStrings__Default 注入；
            // 缺失或为空时退化为不含凭据的本地占位串，保证无数据库访问的场景仍可启动。
            var connectionString = configuration.GetConnectionString("Default");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                connectionString = "Host=localhost;Port=5432;Database=app";
            }

            options.UseNpgsql(connectionString);
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // EF Core 仓储实现（首个业务功能起替换脚手架的内存实现）
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserLoginLogRepository, UserLoginLogRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IInventoryRepository, InventoryRepository>();
        services.AddScoped<IPartnerRepository, PartnerRepository>();
        services.AddScoped<IPurchaseOrderRepository, PurchaseOrderRepository>();
        services.AddScoped<ISalesOrderRepository, SalesOrderRepository>();

        return services;
    }
}
