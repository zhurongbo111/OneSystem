namespace App.Core.Features.PartnerPrices;

/// <summary>
/// 客户价格列表出参（列表页展示：客户 / 商品编码 / 商品名称 / 单位 / 协议价 / 商品销售价对比）。
/// </summary>
public sealed class PartnerPriceListItemDto
{
    /// <summary>协议价 ID</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>客户 ID</summary>
    public string PartnerId { get; init; } = string.Empty;

    /// <summary>客户名称</summary>
    public string PartnerName { get; init; } = string.Empty;

    /// <summary>商品 ID</summary>
    public string ProductId { get; init; } = string.Empty;

    /// <summary>商品编码</summary>
    public string ProductCode { get; init; } = string.Empty;

    /// <summary>商品名称</summary>
    public string ProductName { get; init; } = string.Empty;

    /// <summary>商品计量单位</summary>
    public string Unit { get; init; } = string.Empty;

    /// <summary>协议单价</summary>
    public decimal Price { get; init; }

    /// <summary>商品当前销售价（用于「高于默认价」提示与价差展示）</summary>
    public decimal SalePrice { get; init; }

    /// <summary>备注</summary>
    public string? Remark { get; init; }
}