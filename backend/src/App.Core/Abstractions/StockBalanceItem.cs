namespace App.Core.Abstractions;

/// <summary>
/// 库存余额表行读模型（按分类聚合启用商品，不展开商品行；明细仍走 erp-inventory-query 的库存查询页）。
/// 低库存口径与库存查询页同源：SafetyStock &gt; 0 且 Quantity &lt; SafetyStock。
/// </summary>
public sealed record StockBalanceItem
{
    /// <summary>分类 ID</summary>
    public required Guid CategoryId { get; init; }

    /// <summary>分类名称</summary>
    public required string CategoryName { get; init; }

    /// <summary>分类下启用商品数</summary>
    public required int ProductCount { get; init; }

    /// <summary>分类下库存合计（无库存行按 0 计）</summary>
    public required int TotalQuantity { get; init; }

    /// <summary>分类下零库存商品数</summary>
    public required int ZeroStockCount { get; init; }

    /// <summary>分类下低库存商品数（安全阈值 &gt; 0 且库存 &lt; 阈值）</summary>
    public required int BelowSafetyCount { get; init; }
}
