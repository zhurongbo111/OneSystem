namespace App.Core.Features.Inventory;

/// <summary>
/// 库存查询列表项出参（camelCase 与前端 DTO 一一对应）
/// </summary>
public sealed record InventoryItemDto
{
    /// <summary>商品 ID</summary>
    public required string ProductId { get; init; }

    /// <summary>商品编码</summary>
    public required string Code { get; init; }

    /// <summary>商品名称</summary>
    public required string Name { get; init; }

    /// <summary>分类名称</summary>
    public required string CategoryName { get; init; }

    /// <summary>计量单位</summary>
    public required string Unit { get; init; }

    /// <summary>仓库 ID（038）</summary>
    public required string WarehouseId { get; init; }

    /// <summary>仓库名称（038）</summary>
    public required string WarehouseName { get; init; }

    /// <summary>该仓当前库存（038）</summary>
    public required int StockQuantity { get; init; }

    /// <summary>仓级安全库存阈值（038；判定唯一来源）</summary>
    public required int SafetyStock { get; init; }

    /// <summary>批次 ID（040：展开批次视图有值，汇总视图为 null）</summary>
    public string? BatchId { get; init; }

    /// <summary>批次号（040；汇总视图为 null）</summary>
    public string? BatchNo { get; init; }

    /// <summary>到期日（040；null = 永不过期或汇总视图）</summary>
    public DateTimeOffset? ExpiryDate { get; init; }

    /// <summary>是否已过期（040：到期日 &lt; 今天；汇总视图为 false）</summary>
    public bool IsExpired { get; init; }

    /// <summary>是否近效期（040：到期日 ≤ 今天 + NearExpiryDays 且未过期）</summary>
    public bool IsNearExpiry { get; init; }

    /// <summary>是否低于安全库存（SafetyStock &gt; 0 且 Stock &lt; SafetyStock）</summary>
    public required bool IsBelowSafetyStock { get; init; }

    /// <summary>最近库存变动时间（无库存行时为 null）</summary>
    public DateTimeOffset? UpdatedAt { get; init; }
}
