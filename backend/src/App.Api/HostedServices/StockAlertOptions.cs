using App.Core.Entities;

namespace App.Api.HostedServices;

/// <summary>
/// 库存预警定时任务配置（配置节 <c>StockAlert</c>，specs/041-erp-stock-alert/design.md §3.3）：
/// <see cref="IntervalMinutes"/> 低于 <c>StockAlertFieldConstraints.IntervalMinutesMin</c> 时按下限生效，
/// 避免误配高频扫描；<see cref="Enabled"/> 为 false 时不注册宿主（开发 / e2e 稳定）。
/// </summary>
public sealed class StockAlertOptions
{
    /// <summary>是否启用定时扫描（默认启用）</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>扫描间隔分钟数（默认 60，最小 5）</summary>
    public int IntervalMinutes { get; init; } = StockAlertFieldConstraints.IntervalMinutesDefault;

    /// <summary>启动后首跑延迟秒数（默认 60，避免与启动初始化抢资源）</summary>
    public int StartupDelaySeconds { get; init; } = StockAlertFieldConstraints.StartupDelaySecondsDefault;

    /// <summary>
    /// 从配置节 <c>StockAlert</c> 读取并规范化取值（缺失取默认值；间隔取下限；延迟取非负）
    /// </summary>
    /// <param name="configuration">应用配置</param>
    public static StockAlertOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("StockAlert");

        var intervalMinutes = section.GetValue("IntervalMinutes", StockAlertFieldConstraints.IntervalMinutesDefault);
        if (intervalMinutes < StockAlertFieldConstraints.IntervalMinutesMin)
        {
            intervalMinutes = StockAlertFieldConstraints.IntervalMinutesMin;
        }

        var startupDelaySeconds = section.GetValue("StartupDelaySeconds", StockAlertFieldConstraints.StartupDelaySecondsDefault);
        if (startupDelaySeconds < 0)
        {
            startupDelaySeconds = 0;
        }

        return new StockAlertOptions
        {
            Enabled = section.GetValue("Enabled", true),
            IntervalMinutes = intervalMinutes,
            StartupDelaySeconds = startupDelaySeconds,
        };
    }
}
