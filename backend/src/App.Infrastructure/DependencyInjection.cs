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
    /// 连接串来自 ConnectionStrings:Default（环境变量 ConnectionStrings__Default 注入），未配置时允许启动。
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            // 连接串来自环境变量 ConnectionStrings__Default；未配置时用本地默认占位，
            // 脚手架阶段无实体查询不会发起连接，首个业务功能接入时要求必须配置真实连接串。
            var connectionString = configuration.GetConnectionString("Default")
                ?? "Host=localhost;Port=5432;Database=app;Username=app;Password=app";
            options.UseNpgsql(connectionString);
        });

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // 脚手架阶段为内存实现（InMemoryUserRepository），首个业务功能替换为 EF Core 实现
        services.AddScoped<IUserRepository, InMemoryUserRepository>();

        return services;
    }
}
