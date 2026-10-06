using App.Api.HostedServices;
using App.Core.Entities;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace App.Tests;

/// <summary>
/// 库存预警定时宿主配置单测（041 §3.3 / §6）：
/// <c>Enabled = false</c> 时不注册宿主；间隔低于下限按下限生效；缺失配置取默认值。
/// </summary>
public class StockAlertHostedServiceTests
{
    private static IServiceCollection Register(params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(setting => setting.Key, setting => (string?)setting.Value))
            .Build();

        return new ServiceCollection().AddStockAlertHostedService(configuration);
    }

    [Fact]
    public void 启用时_应注册宿主并解析配置()
    {
        var services = Register(
            ("StockAlert:Enabled", "true"),
            ("StockAlert:IntervalMinutes", "120"),
            ("StockAlert:StartupDelaySeconds", "30"));

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IHostedService));
        var options = services.Single(descriptor => descriptor.ServiceType == typeof(StockAlertOptions)).ImplementationInstance;
        var stockAlertOptions = Assert.IsType<StockAlertOptions>(options);
        Assert.True(stockAlertOptions.Enabled);
        Assert.Equal(120, stockAlertOptions.IntervalMinutes);
        Assert.Equal(30, stockAlertOptions.StartupDelaySeconds);
    }

    [Fact]
    public void 未配置时_应取默认值()
    {
        var services = Register();

        var options = services.Single(descriptor => descriptor.ServiceType == typeof(StockAlertOptions)).ImplementationInstance;
        var stockAlertOptions = Assert.IsType<StockAlertOptions>(options);
        Assert.Equal(StockAlertFieldConstraints.IntervalMinutesDefault, stockAlertOptions.IntervalMinutes);
        Assert.Equal(StockAlertFieldConstraints.StartupDelaySecondsDefault, stockAlertOptions.StartupDelaySeconds);
    }

    [Fact]
    public void 禁用时_不应注册宿主()
    {
        var services = Register(("StockAlert:Enabled", "false"));

        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(IHostedService));
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(StockAlertOptions));
    }

    [Fact]
    public void 间隔低于下限_应按下限规范化()
    {
        var services = Register(("StockAlert:IntervalMinutes", "1"));

        var options = services.Single(descriptor => descriptor.ServiceType == typeof(StockAlertOptions)).ImplementationInstance;
        var stockAlertOptions = Assert.IsType<StockAlertOptions>(options);
        Assert.Equal(StockAlertFieldConstraints.IntervalMinutesMin, stockAlertOptions.IntervalMinutes);
    }
}
