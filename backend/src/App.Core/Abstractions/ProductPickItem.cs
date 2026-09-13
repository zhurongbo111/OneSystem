namespace App.Core.Abstractions;

/// <summary>
/// 开单商品选择读模型（仅启用商品，供 erp-purchase / erp-sale 开单页消费，全量返回）
/// </summary>
public sealed record ProductPickItem
{
    /// <summary>商品 ID</summary>
    public required Guid Id { get; init; }

    /// <summary>商品编码</summary>
    public required string Code { get; init; }

    /// <summary>商品名称</summary>
    public required string Name { get; init; }

    /// <summary>计量单位</summary>
    public required string Unit { get; init; }

    /// <summary>默认采购价</summary>
    public required decimal PurchasePrice { get; init; }

    /// <summary>默认销售价</summary>
    public required decimal SalePrice { get; init; }

    /// <summary>当前库存（联查 Inventory 带出）</summary>
    public required int StockQuantity { get; init; }
}
