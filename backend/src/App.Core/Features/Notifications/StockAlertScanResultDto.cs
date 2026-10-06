namespace App.Core.Features.Notifications;

/// <summary>
/// 库存预警扫描结果出参（specs/041-erp-stock-alert/design.md §0 / §3.5）：
/// 「信号数 / 生成消息数 / 跳过（去重）数 / 接收人数」+ 各类型信号数；手动扫描与定时宿主共用同一实现。
/// </summary>
public sealed class StockAlertScanResultDto
{
    /// <summary>本次扫描到的信号总数（含被单次上限截断的部分）</summary>
    public int SignalCount { get; init; }

    /// <summary>低库存信号数（截断后实际处理数）</summary>
    public int LowStockCount { get; init; }

    /// <summary>近效期批次信号数（截断后实际处理数）</summary>
    public int ExpiringBatchCount { get; init; }

    /// <summary>已过期批次信号数（截断后实际处理数）</summary>
    public int ExpiredBatchCount { get; init; }

    /// <summary>本次生成的站内信条数（= 处理信号数 × 接收人数）</summary>
    public int MessageCount { get; init; }

    /// <summary>当日去重跳过的信号数</summary>
    public int SkippedCount { get; init; }

    /// <summary>接收人数量（持有 inventory.view 权限的启用用户）</summary>
    public int RecipientCount { get; init; }

    /// <summary>是否因超过单次扫描上限而被截断（下轮继续）</summary>
    public bool Truncated { get; init; }
}
