namespace App.Core.Abstractions;

/// <summary>
/// 库存预警信号读模型（规格 specs/041-erp-stock-alert/design.md §3.1，唯一事实源：§0 判定表）。
/// 供扫描器把「只读查询结果」转换为站内信，属仓储出参契约（不暴露到 API）。
/// 三类信号共用：低库存信号 <see cref="BatchId"/> / <see cref="BatchNo"/> / <see cref="ExpiryDate"/> 为空，
/// 批次信号携带该三个字段（<see cref="Quantity"/> 为该「商品 × 仓 × 批次」行的库存）。
/// </summary>
public sealed record StockAlertSignal
{
    /// <summary>商品 ID</summary>
    public required Guid ProductId { get; init; }

    /// <summary>商品编码（联查 Products 带出）</summary>
    public required string ProductCode { get; init; }

    /// <summary>商品名称（联查 Products 带出）</summary>
    public required string ProductName { get; init; }

    /// <summary>仓库 ID</summary>
    public required Guid WarehouseId { get; init; }

    /// <summary>仓库名称（联查 Warehouses 带出）</summary>
    public required string WarehouseName { get; init; }

    /// <summary>当前库存（低库存信号 = 该「商品 × 仓」Σ 各批次行数量；批次信号 = 该批次行数量）</summary>
    public required int Quantity { get; init; }

    /// <summary>仓级安全库存阈值（低库存信号 = 该「商品 × 仓」MAX(SafetyStock)；批次信号恒为 0，不参与判定）</summary>
    public required int SafetyStock { get; init; }

    /// <summary>批次 ID（仅批次信号有值）</summary>
    public Guid? BatchId { get; init; }

    /// <summary>批次号（仅批次信号有值）</summary>
    public string? BatchNo { get; init; }

    /// <summary>到期日（UTC 午夜，仅批次信号有值；为空 = 永不过期）</summary>
    public DateTimeOffset? ExpiryDate { get; init; }
}
