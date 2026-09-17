namespace App.Core.Features.Reports;

/// <summary>
/// 库存余额表行出参（按分类聚合，对应读模型 <c>StockBalanceItem</c>）。
/// </summary>
public sealed class StockBalanceItemDto
{
    /// <summary>分类 ID</summary>
    public string CategoryId { get; init; } = string.Empty;

    /// <summary>分类名称</summary>
    public string CategoryName { get; init; } = string.Empty;

    /// <summary>分类下启用商品数</summary>
    public int ProductCount { get; init; }

    /// <summary>分类下库存合计</summary>
    public int TotalQuantity { get; init; }

    /// <summary>分类下零库存商品数</summary>
    public int ZeroStockCount { get; init; }

    /// <summary>分类下低库存商品数</summary>
    public int BelowSafetyCount { get; init; }

    /// <summary>库存占比（该分类库存占筛选结果全量的比例，0–1，由 Mapper 计算；全库为 0 时为 0）</summary>
    public decimal QuantityRatio { get; init; }
}

/// <summary>
/// 库存余额表合计出参（全量筛选结果口径）。
/// </summary>
public sealed class StockBalanceSummaryDto
{
    /// <summary>商品总数</summary>
    public int ProductCount { get; init; }

    /// <summary>库存总量</summary>
    public int TotalQuantity { get; init; }

    /// <summary>零库存商品数</summary>
    public int ZeroStockCount { get; init; }

    /// <summary>低库存商品数</summary>
    public int BelowSafetyCount { get; init; }
}
