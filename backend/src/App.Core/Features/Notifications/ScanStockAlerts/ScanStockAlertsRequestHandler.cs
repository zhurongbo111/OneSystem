using App.Core.Abstractions;
using App.Core.Alerts;

namespace App.Core.Features.Notifications.ScanStockAlerts;

/// <summary>
/// 手动触发库存预警扫描用例：调用 <see cref="IStockAlertScanner"/>（与定时宿主同一实现），
/// 扫描时刻由用例读取系统时钟注入（扫描器本身不读时钟，便于单测）。
/// 无业务失败分支：无接收人 / 无信号都返回 0 统计（design.md §3.4）。
/// </summary>
public sealed class ScanStockAlertsRequestHandler : IRequestHandler<ScanStockAlertsRequest, StockAlertScanResultDto>
{
    private readonly IStockAlertScanner _scanner;

    /// <summary>
    /// 初始化手动扫描用例处理器
    /// </summary>
    public ScanStockAlertsRequestHandler(IStockAlertScanner scanner)
    {
        _scanner = scanner;
    }

    /// <summary>
    /// 处理手动扫描请求
    /// </summary>
    /// <param name="request">空请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public Task<StockAlertScanResultDto> HandleAsync(
        ScanStockAlertsRequest request, CancellationToken cancellationToken = default)
        => _scanner.ScanAsync(DateTimeOffset.UtcNow, cancellationToken);
}
