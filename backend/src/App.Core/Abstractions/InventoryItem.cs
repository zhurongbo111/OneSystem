namespace App.Core.Abstractions;

/// <summary>
/// 库存查询列表项读模型（联查 Products / Categories 带出，不暴露实体）。
/// 低库存标记 isBelowSafetyStock 由 Handler 计算，仓储只负责联查出原始值。
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

    /// <summary>当前库存（联查 Inventory 带出，无库存行时按 0 计）</summary>
    public required int StockQuantity { get; init; }

    /// <summary>安全库存阈值</summary>
    public required int SafetyStock { get; init; }

    /// <summary>最近库存变动时间</summary>
    public DateTimeOffset? UpdatedAt { get; init; }
}
