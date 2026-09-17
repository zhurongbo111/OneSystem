namespace App.Core.Abstractions;

/// <summary>
/// 进销存报表行读模型（联查 Products / Categories 带出编码 / 名称 / 分类 / 单位，不暴露实体）。
/// 期初 / 期间入 / 期间出取自 StockMovements 聚合，期末为推导列（期初 + 入 − 出），
/// 口径见 specs/025-erp-report/design.md §0.1。
/// </summary>
public sealed record InventoryFlowItem
{
    /// <summary>商品 ID</summary>
    public required Guid ProductId { get; init; }

    /// <summary>商品编码（联查 Products 带出）</summary>
    public required string Code { get; init; }

    /// <summary>商品名称（联查 Products 带出）</summary>
    public required string Name { get; init; }

    /// <summary>分类名称（联查 Categories 带出）</summary>
    public required string CategoryName { get; init; }

    /// <summary>计量单位（联查 Products 带出）</summary>
    public required string Unit { get; init; }

    /// <summary>期初数量（期间起点之前的全部流水累计）</summary>
    public required int OpeningQuantity { get; init; }

    /// <summary>期间入（入向变动类型合计；盘点调整为负的部分不计入本列）</summary>
    public required int InboundQuantity { get; init; }

    /// <summary>期间出（出向变动类型合计的绝对值；盘点调整为正的部分不计入本列）</summary>
    public required int OutboundQuantity { get; init; }

    /// <summary>期末数量 = 期初 + 期间入 − 期间出（仓储内计算，保证与对账断言一致）</summary>
    public required int ClosingQuantity { get; init; }
}
