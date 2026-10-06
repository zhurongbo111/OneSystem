using System.Globalization;
using System.Text.Json;

using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Entities;
using App.Core.Features.Notifications;

using Microsoft.Extensions.Logging;

namespace App.Core.Alerts;

/// <summary>
/// 库存预警扫描器（specs/041-erp-stock-alert/design.md §3.2）：定时宿主与手动「立即扫描」共用同一实现。
/// 流程：取接收人（inventory.view 权限的启用用户）→ 取三类信号（受单次上限约束）→ 逐条去重（库 + 本批）
/// → 为每个接收人构造站内信 → **先写消息、后写台账**（极端失败下宁可重复告警也不漏告警）。
/// 判定口径唯一来源见 design.md §0；文案为固定中文模板（不做模板管理，范围外）。
/// </summary>
public sealed class StockAlertScanner : IStockAlertScanner
{
    /// <summary>低库存告警跳转路由名（前端 route name）</summary>
    private const string InventoryRouteName = "inventory";

    /// <summary>批次告警跳转路由名（前端 route name）</summary>
    private const string BatchesRouteName = "batches";

    private const string LowStockTitle = "库存不足提醒";
    private const string ExpiringBatchTitle = "批次近效期提醒";
    private const string ExpiredBatchTitle = "批次已过期提醒";

    private readonly IStockAlertQueryRepository _queryRepository;
    private readonly IAlertRecordRepository _alertRecordRepository;
    private readonly IPermissionedUserQuery _permissionedUserQuery;
    private readonly INotificationWriter _notificationWriter;
    private readonly ILogger<StockAlertScanner> _logger;

    /// <summary>
    /// 初始化库存预警扫描器
    /// </summary>
    public StockAlertScanner(
        IStockAlertQueryRepository queryRepository,
        IAlertRecordRepository alertRecordRepository,
        IPermissionedUserQuery permissionedUserQuery,
        INotificationWriter notificationWriter,
        ILogger<StockAlertScanner> logger)
    {
        _queryRepository = queryRepository;
        _alertRecordRepository = alertRecordRepository;
        _permissionedUserQuery = permissionedUserQuery;
        _notificationWriter = notificationWriter;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<StockAlertScanResultDto> ScanAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default)
    {
        var alertDate = DateOnly.FromDateTime(utcNow.UtcDateTime);

        // 接收人 = 持有 inventory.view 权限的启用用户（028 权限数据反向查询）
        var recipientIds = await _permissionedUserQuery
            .GetEnabledUserIdsByPermissionAsync(Permissions.InventoryView, cancellationToken);

        if (recipientIds.Count == 0)
        {
            _logger.LogWarning(
                "库存预警扫描跳过：无持有 {Permission} 权限的启用用户（告警日期 {AlertDate}）",
                Permissions.InventoryView,
                alertDate);
            return new StockAlertScanResultDto();
        }

        var signals = await LoadSignalsAsync(alertDate, cancellationToken);
        var signalCount = signals.Count;
        var truncated = false;

        // 单次扫描上限：超出只处理前 N 条并记 warning，下轮继续（防异常数据量把一次扫描撑爆）
        if (signals.Count > StockAlertFieldConstraints.MaxSignalsPerScan)
        {
            truncated = true;
            _logger.LogWarning(
                "库存预警信号数 {Count} 超过单次上限 {Max}，本轮只处理前 {Max} 条（告警日期 {AlertDate}）",
                signals.Count,
                StockAlertFieldConstraints.MaxSignalsPerScan,
                StockAlertFieldConstraints.MaxSignalsPerScan,
                alertDate);
            signals = signals.Take(StockAlertFieldConstraints.MaxSignalsPerScan).ToList();
        }

        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        var newRecords = new List<AlertRecord>();
        var messages = new List<Notification>();
        var skippedCount = 0;
        var lowStockCount = 0;
        var expiringBatchCount = 0;
        var expiredBatchCount = 0;

        foreach (var item in signals)
        {
            switch (item.AlertType)
            {
                case AlertType.LowStock:
                    lowStockCount++;
                    break;
                case AlertType.ExpiringBatch:
                    expiringBatchCount++;
                    break;
                default:
                    expiredBatchCount++;
                    break;
            }

            var resourceKey = BuildResourceKey(item);

            // 当日去重：本批内重复 + 库中已告警均跳过（库层唯一索引为并发兜底）
            if (!seenKeys.Add(resourceKey)
                || await _alertRecordRepository.ExistsAsync(item.AlertType, resourceKey, alertDate, cancellationToken))
            {
                skippedCount++;
                continue;
            }

            foreach (var userId in recipientIds)
            {
                messages.Add(BuildNotification(userId, item, alertDate, utcNow, resourceKey));
            }

            newRecords.Add(new AlertRecord
            {
                Id = Guid.NewGuid(),
                AlertType = item.AlertType,
                ResourceKey = resourceKey,
                AlertDate = alertDate,
                CreatedAt = utcNow,
            });
        }

        // 先写消息、后写台账：消息重复优于消息丢失（design.md §5）
        await _notificationWriter.WriteAsync(messages, cancellationToken);
        await _alertRecordRepository.AddRangeAsync(newRecords, cancellationToken);

        _logger.LogInformation(
            "库存预警扫描完成：信号 {SignalCount}（低库存 {LowStock} / 近效期 {Expiring} / 过期 {Expired}）、"
            + "生成消息 {MessageCount}、去重跳过 {SkippedCount}、接收人 {RecipientCount}、截断 {Truncated}",
            signalCount,
            lowStockCount,
            expiringBatchCount,
            expiredBatchCount,
            messages.Count,
            skippedCount,
            recipientIds.Count,
            truncated);

        return new StockAlertScanResultDto
        {
            SignalCount = signalCount,
            LowStockCount = lowStockCount,
            ExpiringBatchCount = expiringBatchCount,
            ExpiredBatchCount = expiredBatchCount,
            MessageCount = messages.Count,
            SkippedCount = skippedCount,
            RecipientCount = recipientIds.Count,
            Truncated = truncated,
        };
    }

    /// <summary>
    /// 取三类信号（各查询自带上限），按「低库存 → 近效期 → 过期」顺序合并
    /// </summary>
    private async Task<List<AlertSignalItem>> LoadSignalsAsync(DateOnly alertDate, CancellationToken cancellationToken)
    {
        var maxCount = StockAlertFieldConstraints.MaxSignalsPerScan;

        var lowStock = await _queryRepository.GetLowStockSignalsAsync(maxCount, cancellationToken);
        var expiring = await _queryRepository.GetExpiringBatchSignalsAsync(
            alertDate, BatchFieldConstraints.NearExpiryDays, maxCount, cancellationToken);
        var expired = await _queryRepository.GetExpiredBatchSignalsAsync(alertDate, maxCount, cancellationToken);

        var signals = new List<AlertSignalItem>(lowStock.Count + expiring.Count + expired.Count);
        signals.AddRange(lowStock.Select(signal => new AlertSignalItem(AlertType.LowStock, signal)));
        signals.AddRange(expiring.Select(signal => new AlertSignalItem(AlertType.ExpiringBatch, signal)));
        signals.AddRange(expired.Select(signal => new AlertSignalItem(AlertType.ExpiredBatch, signal)));
        return signals;
    }

    /// <summary>按信号类型拼装去重键：低库存 <c>product:&lt;id&gt;:warehouse:&lt;id&gt;</c>；批次 <c>batch:&lt;id&gt;:warehouse:&lt;id&gt;</c></summary>
    private static string BuildResourceKey(AlertSignalItem item)
        => item.AlertType == AlertType.LowStock
            ? $"product:{item.Signal.ProductId}:warehouse:{item.Signal.WarehouseId}"
            : $"batch:{item.Signal.BatchId}:warehouse:{item.Signal.WarehouseId}";

    /// <summary>按信号类型构造站内信（标题 / 内容 / 跳转目标）</summary>
    private static Notification BuildNotification(
        Guid userId, AlertSignalItem item, DateOnly alertDate, DateTimeOffset utcNow, string resourceKey)
    {
        var signal = item.Signal;

        return item.AlertType switch
        {
            AlertType.LowStock => new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Type = NotificationType.LowStock,
                Title = LowStockTitle,
                Content = $"{signal.WarehouseName} {signal.ProductName}（{signal.ProductCode}）"
                    + $"当前库存 {signal.Quantity}，低于安全库存 {signal.SafetyStock}",
                LinkRouteName = InventoryRouteName,
                LinkQuery = BuildQuery(new Dictionary<string, string>
                {
                    ["warehouseId"] = signal.WarehouseId.ToString(),
                    ["keyword"] = signal.ProductCode,
                }),
                ResourceKey = resourceKey,
                CreatedAt = utcNow,
            },
            AlertType.ExpiringBatch => new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Type = NotificationType.ExpiringBatch,
                Title = ExpiringBatchTitle,
                Content = $"{signal.ProductName}（{signal.ProductCode}）批次 {signal.BatchNo} "
                    + $"将于 {FormatDate(signal.ExpiryDate)} 到期（剩 {DaysUntil(signal.ExpiryDate, alertDate)} 天），"
                    + $"当前库存 {signal.Quantity}",
                LinkRouteName = BatchesRouteName,
                LinkQuery = BuildQuery(new Dictionary<string, string>
                {
                    ["productId"] = signal.ProductId.ToString(),
                    ["keyword"] = signal.BatchNo ?? string.Empty,
                }),
                ResourceKey = resourceKey,
                CreatedAt = utcNow,
            },
            _ => new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Type = NotificationType.ExpiredBatch,
                Title = ExpiredBatchTitle,
                Content = $"{signal.ProductName}（{signal.ProductCode}）批次 {signal.BatchNo} "
                    + $"已于 {FormatDate(signal.ExpiryDate)} 过期，当前库存 {signal.Quantity}，请及时处理",
                LinkRouteName = BatchesRouteName,
                LinkQuery = BuildQuery(new Dictionary<string, string>
                {
                    ["productId"] = signal.ProductId.ToString(),
                    ["keyword"] = signal.BatchNo ?? string.Empty,
                }),
                ResourceKey = resourceKey,
                CreatedAt = utcNow,
            },
        };
    }

    /// <summary>跳转 query 序列化为 JSON 文本（前端 <c>JSON.parse</c> 后作为 <c>router.push</c> 的 query）</summary>
    private static string BuildQuery(Dictionary<string, string> query)
        => JsonSerializer.Serialize(query);

    /// <summary>到期日格式化为 <c>yyyy-MM-dd</c>（UTC 日期）</summary>
    private static string FormatDate(DateTimeOffset? expiryDate)
        => expiryDate is null
            ? string.Empty
            : expiryDate.Value.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>距到期日剩余天数（日期粒度，可为负）</summary>
    private static int DaysUntil(DateTimeOffset? expiryDate, DateOnly today)
        => expiryDate is null
            ? 0
            : DateOnly.FromDateTime(expiryDate.Value.UtcDateTime).DayNumber - today.DayNumber;

    /// <summary>扫描信号（类型 + 只读查询结果）</summary>
    private sealed record AlertSignalItem(AlertType AlertType, StockAlertSignal Signal);
}
