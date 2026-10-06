using App.Core.Abstractions;

namespace App.Core.Features.Notifications.ScanStockAlerts;

/// <summary>
/// 手动触发库存预警扫描请求（无请求参数；与定时宿主共用同一扫描实现）
/// </summary>
public sealed class ScanStockAlertsRequest : IRequest<StockAlertScanResultDto>
{
}
