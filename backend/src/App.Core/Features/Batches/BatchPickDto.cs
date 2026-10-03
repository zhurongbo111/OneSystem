namespace App.Core.Features.Batches;

/// <summary>
/// 批次下拉项出参（开单页批次选择控件，含该仓可用库存与过期 / 近效期标记）。
/// </summary>
public sealed class BatchPickDto
{
    /// <summary>批次 ID</summary>
    public string BatchId { get; init; } = string.Empty;

    /// <summary>批次号</summary>
    public string BatchNo { get; init; } = string.Empty;

    /// <summary>生产日期（UTC 午夜，可空）</summary>
    public DateTimeOffset? ProductionDate { get; init; }

    /// <summary>到期日（UTC 午夜，可空；null = 永不过期）</summary>
    public DateTimeOffset? ExpiryDate { get; init; }

    /// <summary>批次状态（0 停用 / 1 启用）</summary>
    public int Status { get; init; }

    /// <summary>该仓可用库存（Σ 该仓该批次行数量）</summary>
    public int AvailableQuantity { get; init; }

    /// <summary>是否已过期（ExpiryDate &lt; 今天）</summary>
    public bool IsExpired { get; init; }

    /// <summary>是否近效期（到期日 ≤ 今天 + 30 天且未过期）</summary>
    public bool IsNearExpiry { get; init; }
}
