namespace App.Core.Features.Batches;

/// <summary>
/// 批次列表行出参（联查商品编码 / 名称 + 跨仓库存合计）。
/// </summary>
public sealed class BatchListItemDto
{
    /// <summary>批次 ID</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>批次所属商品 ID</summary>
    public string ProductId { get; init; } = string.Empty;

    /// <summary>商品编码（联查 Products 带出）</summary>
    public string ProductCode { get; init; } = string.Empty;

    /// <summary>商品名称（联查 Products 带出）</summary>
    public string ProductName { get; init; } = string.Empty;

    /// <summary>批次号</summary>
    public string BatchNo { get; init; } = string.Empty;

    /// <summary>生产日期（UTC 午夜，可空）</summary>
    public DateTimeOffset? ProductionDate { get; init; }

    /// <summary>到期日（UTC 午夜，可空；null = 永不过期）</summary>
    public DateTimeOffset? ExpiryDate { get; init; }

    /// <summary>批次状态（0 停用 / 1 启用）</summary>
    public int Status { get; init; }

    /// <summary>跨仓库存合计（Σ 该批次所有仓批次行）</summary>
    public int TotalStock { get; init; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>备注</summary>
    public string? Remark { get; init; }
}
