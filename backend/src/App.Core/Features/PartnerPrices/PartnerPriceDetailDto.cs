namespace App.Core.Features.PartnerPrices;

/// <summary>
/// 客户价格详情出参（新增 / 编辑 / 详情共用；客户与商品不可改，故读写一致）。
/// </summary>
public sealed class PartnerPriceDetailDto
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

    /// <summary>商品当前销售价（低于协议价时前端提示「高于默认价」）</summary>
    public decimal SalePrice { get; init; }

    /// <summary>备注</summary>
    public string? Remark { get; init; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>更新时间</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}