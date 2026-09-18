namespace App.Core.Abstractions;

/// <summary>
/// 库存余额表合计模型（对**全量筛选结果**聚合，与当前页无关；「全库合计」语义见 specs/025-erp-report design.md §3.4）。
/// </summary>
public sealed record StockBalanceTotal
{
    /// <summary>商品总数（启用商品）</summary>
    public required int ProductCount { get; init; }

    /// <summary>库存总量</summary>
    public required int TotalQuantity { get; init; }

    /// <summary>零库存商品数</summary>
    public required int ZeroStockCount { get; init; }

    /// <summary>低库存商品数</summary>
    public required int BelowSafetyCount { get; init; }

    /// <summary>库存成本额合计（erp-cost）</summary>
    public required decimal TotalCostAmount { get; init; }
}
