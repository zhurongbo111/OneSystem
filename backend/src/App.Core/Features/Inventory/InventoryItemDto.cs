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

    /// <summary>当前库存</summary>
    public required int StockQuantity { get; init; }

    /// <summary>安全库存阈值</summary>
    public required int SafetyStock { get; init; }

    /// <summary>是否低于安全库存（SafetyStock &gt; 0 且 Stock &lt; SafetyStock）</summary>
    public required bool IsBelowSafetyStock { get; init; }

    /// <summary>最近库存变动时间（无库存行时为 null）</summary>
    public DateTimeOffset? UpdatedAt { get; init; }
}
