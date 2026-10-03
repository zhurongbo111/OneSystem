using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 批次列表项读模型（联查 Products / Inventory 带出，不暴露实体；040-erp-batch-expiry/design.md §3.1）。
/// 一行 = 一个批次；库存合计为**跨仓汇总**（Σ 各仓批次行）。
/// </summary>
public sealed record BatchListItem
{
    /// <summary>批次 ID</summary>
    public required Guid Id { get; init; }

    /// <summary>批次所属商品 ID</summary>
    public required Guid ProductId { get; init; }

    /// <summary>商品编码（联查 Products 带出）</summary>
    public required string ProductCode { get; init; }

    /// <summary>商品名称（联查 Products 带出）</summary>
    public required string ProductName { get; init; }

    /// <summary>批次号</summary>
    public required string BatchNo { get; init; }

    /// <summary>生产日期（UTC 午夜，可空）</summary>
    public DateTimeOffset? ProductionDate { get; init; }

    /// <summary>到期日（UTC 午夜，可空；null = 永不过期）</summary>
    public DateTimeOffset? ExpiryDate { get; init; }

    /// <summary>批次状态</summary>
    public required PartnerStatus Status { get; init; }

    /// <summary>跨仓库存合计（Σ 该批次所有仓批次行）</summary>
    public required int TotalStock { get; init; }

    /// <summary>最早到期日（该批次只有一个到期日即其 ExpiryDate；保留字段供批次维度视图扩展）</summary>
    public DateTimeOffset? EarliestExpiryDate { get; init; }

    /// <summary>创建时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>备注</summary>
    public string? Remark { get; init; }
}
