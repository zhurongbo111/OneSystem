namespace App.Core.Entities;

/// <summary>
/// 库存预警扫描常量（单一来源，specs/041-erp-stock-alert/design.md §3.1 / §3.3）：
/// 单次扫描信号上限由扫描器与仓储共用；轮询间隔 / 首跑延迟由宿主配置节读取，越界取下限。
/// </summary>
public static class StockAlertFieldConstraints
{
    /// <summary>单次扫描处理的信号上限（超出记 warning，只处理前 N 条，下轮继续）</summary>
    public const int MaxSignalsPerScan = 500;

    /// <summary>扫描间隔默认分钟数</summary>
    public const int IntervalMinutesDefault = 60;

    /// <summary>扫描间隔下限（配置小于该值按该值生效，避免高频扫描）</summary>
    public const int IntervalMinutesMin = 5;

    /// <summary>宿主启动后首跑延迟默认秒数</summary>
    public const int StartupDelaySecondsDefault = 60;
}
