namespace App.Core.Abstractions;

/// <summary>
/// 库存查询列表项读模型（联查 Products / Categories / Warehouses 带出，不暴露实体）。
/// 一行 = 一个「商品 × 仓库」（038 维度升级）；低库存标记 isBelowSafetyStock 由 Handler 计算，
/// 仓储只负责联查出原始值。
/// </summary>
public sealed record InventoryItem
{
    /// <summary>商品 ID</summary>
    public required Guid ProductId { get; init; }

    /// <summary>商品编码</summary>
    public required string Code { get; init; }

    /// <summary>商品名称</summary>
    public required string Name { get; init; }

    /// <summary>分类名称（联查带出）</summary>
    public required string CategoryName { get; init; }

    /// <summary>计量单位</summary>
    public required string Unit { get; init; }

    /// <summary>仓库 ID（038）</summary>
    public required Guid WarehouseId { get; init; }

    /// <summary>仓库名称（联查 Warehouses 带出）</summary>
    public required string WarehouseName { get; init; }

    /// <summary>该仓当前库存（联查 Inventory 带出；展开批次视图 = 该批次行数量，汇总视图 = 批次合计）</summary>
    public required int StockQuantity { get; init; }

    /// <summary>仓级安全库存阈值（038；汇总视图 = 组合下 MAX，展开批次视图不使用，前端隐藏该列）</summary>
    public required int SafetyStock { get; init; }

    /// <summary>批次 ID（040；展开批次视图有值，汇总视图与历史行为为 null）</summary>
    public Guid? BatchId { get; init; }

    /// <summary>批次号（展开批次视图联查 Batches 带出；汇总视图为 null）</summary>
    public string? BatchNo { get; init; }

    /// <summary>到期日（展开批次视图联查 Batches 带出；null = 永不过期或汇总视图）</summary>
    public DateTimeOffset? ExpiryDate { get; init; }

    /// <summary>最近库存变动时间</summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>商品创建时间（erp-export 导出「创建时间」列用，联查 Products 带出）</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>商品创建人 id（erp-export 导出「创建人」列用，经 IUserRepository 批量换显示名）</summary>
    public required Guid? CreatedBy { get; init; }
}
