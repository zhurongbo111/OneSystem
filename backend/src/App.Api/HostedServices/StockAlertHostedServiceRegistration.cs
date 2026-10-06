namespace App.Api.HostedServices;

/// <summary>
/// 库存预警定时宿主的条件注册（specs/041-erp-stock-alert/design.md §3.3）：
/// <c>StockAlert:Enabled = false</c> 时不注册宿主（开发与 e2e 不跑定时任务）；
/// 启用时把规范化后的 <see cref="StockAlertOptions"/> 一并注册供宿主读取。
/// </summary>
public static class StockAlertHostedServiceRegistration
{
    /// <summary>
    /// 按配置注册库存预警定时宿主
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configuration">应用配置</param>
    public static IServiceCollection AddStockAlertHostedService(
        this IServiceCollection services, IConfiguration configuration)
    {
        var options = StockAlertOptions.FromConfiguration(configuration);
        if (!options.Enabled)
        {
            return services;
        }

        services.AddSingleton(options);
        services.AddHostedService<StockAlertBackgroundService>();
        return services;
    }
}
