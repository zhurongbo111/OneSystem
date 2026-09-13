namespace App.Core.Features.Products.GetProductPickList;

/// <summary>
/// 开单商品选择出参模型（仅启用商品，供 erp-purchase / erp-sale 开单页消费）
/// </summary>
public sealed class ProductPickDto
{
    /// <summary>商品 ID</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>商品编码</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>商品名称</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>计量单位</summary>
    public string Unit { get; init; } = string.Empty;

    /// <summary>默认采购价</summary>
    public decimal PurchasePrice { get; init; }

    /// <summary>默认销售价</summary>
    public decimal SalePrice { get; init; }

    /// <summary>当前库存</summary>
    public int StockQuantity { get; init; }
}
