using App.Core.Alerts;

namespace App.Api.HostedServices;

/// <summary>
/// 库存预警定时宿主（specs/041-erp-stock-alert/design.md §3.3）：薄封装，只负责周期调度与异常隔离，
/// **业务在 <see cref="IStockAlertScanner"/>（App.Core）**。启动后延迟首跑，随后按间隔循环；
/// 每轮 try/catch 捕获全部异常并记 error，不终止循环；扫描时刻在此读取（唯一允许读时钟的位置）。
/// </summary>
public sealed class StockAlertBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly StockAlertOptions _options;
    private readonly ILogger<StockAlertBackgroundService> _logger;

    /// <summary>
    /// 初始化库存预警定时宿主
    /// </summary>
    public StockAlertBackgroundService(
        IServiceScopeFactory scopeFactory,
        StockAlertOptions options,
        ILogger<StockAlertBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "库存预警定时任务启动：间隔 {IntervalMinutes} 分钟，首跑延迟 {StartupDelaySeconds} 秒",
            _options.IntervalMinutes,
            _options.StartupDelaySeconds);

        if (!await DelayAsync(TimeSpan.FromSeconds(_options.StartupDelaySeconds), stoppingToken))
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var scanner = scope.ServiceProvider.GetRequiredService<IStockAlertScanner>();
                var result = await scanner.ScanAsync(DateTimeOffset.UtcNow, stoppingToken);

                _logger.LogInformation(
                    "库存预警定时扫描完成：信号 {SignalCount}、生成消息 {MessageCount}、跳过 {SkippedCount}、接收人 {RecipientCount}",
                    result.SignalCount,
                    result.MessageCount,
                    result.SkippedCount,
                    result.RecipientCount);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                // 单次失败不影响后续周期（异常隔离）
                _logger.LogError(exception, "库存预警定时扫描失败，将在下一周期重试");
            }

            if (!await DelayAsync(TimeSpan.FromMinutes(_options.IntervalMinutes), stoppingToken))
            {
                break;
            }
        }
    }

    /// <summary>等待指定时长；服务停止时返回 false（不再进入下一轮）</summary>
    private static async Task<bool> DelayAsync(TimeSpan delay, CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(delay, stoppingToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
