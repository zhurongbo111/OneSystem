using App.Core.Features.Notifications;

namespace App.Core.Alerts;

/// <summary>
/// 库存预警扫描能力（specs/041-erp-stock-alert/design.md §1 / §3.2）：
/// 纯业务组件，扫描「低库存 / 近效期 / 已过期」三类信号 → 当日去重 → 生成站内信 → 返回统计。
/// 时间由入参注入（不读时钟，便于单测）；只读业务数据、只追加消息与台账（不改任何业务数据）。
/// </summary>
public interface IStockAlertScanner
{
    /// <summary>
    /// 执行一次扫描
    /// </summary>
    /// <param name="utcNow">本次扫描时刻（由调用方注入：定时宿主传 <c>DateTimeOffset.UtcNow</c>）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task<StockAlertScanResultDto> ScanAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default);
}
