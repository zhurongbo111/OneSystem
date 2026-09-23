namespace App.Core.Abstractions;

/// <summary>
/// 客户价格列表读模型（仓储出参契约，联查往来单位与商品；不暴露到 API）。
/// </summary>
public sealed record PartnerPriceListItem
{
    /// <summary>协议价 id</summary>
    public required Guid Id { get; init; }

    /// <summary>客户 id</summary>
    public required Guid PartnerId { get; init; }

    /// <summary>客户名称</summary>
    public required string PartnerName { get; init; }

    /// <summary>商品 id</summary>
    public required Guid ProductId { get; init; }

    /// <summary>商品编码</summary>
    public required string ProductCode { get; init; }

    /// <summary>商品名称</summary>
    public required string ProductName { get; init; }

    /// <summary>商品计量单位（商品档案当前值，用于列表展示）</summary>
    public required string Unit { get; init; }

    /// <summary>协议单价</summary>
    public required decimal Price { get; init; }

    /// <summary>商品当前销售价（用于列表对比展示）</summary>
    public required decimal SalePrice { get; init; }

    /// <summary>备注</summary>
    public string? Remark { get; init; }
}