using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 批次下拉项读模型（开单页批次选择控件用，040-erp-batch-expiry/design.md §3.1）。
/// 一行 = 一个启用批次（含该仓可用库存）；<c>IsExpired</c> / <c>IsNearExpiry</c> 由 Handler 按固定「今天」计算（仓储不做判定）。
/// </summary>
public sealed record BatchPickItem
{
    /// <summary>批次 ID</summary>
    public required Guid BatchId { get; init; }

    /// <summary>批次号</summary>
    public required string BatchNo { get; init; }

    /// <summary>生产日期（UTC 午夜，可空）</summary>
    public DateTimeOffset? ProductionDate { get; init; }

    /// <summary>到期日（UTC 午夜，可空；null = 永不过期）</summary>
    public DateTimeOffset? ExpiryDate { get; init; }

    /// <summary>批次状态（启用 / 停用）</summary>
    public required PartnerStatus Status { get; init; }

    /// <summary>该仓可用库存（Σ 该仓该批次行数量；批次在该仓无库存行为 0）</summary>
    public required int AvailableQuantity { get; init; }

    /// <summary>是否已过期（ExpiryDate &lt; 今天；仓储返回初始 false，Handler 覆写）</summary>
    public bool IsExpired { get; init; }

    /// <summary>是否近效期（到期日不晚于 今天 + NearExpiryDays 且未过期；Handler 覆写）</summary>
    public bool IsNearExpiry { get; init; }
}
