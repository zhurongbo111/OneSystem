namespace App.Core.Features.PartnerPrices;

/// <summary>
/// 生效价出参（销售开单批量取价：商品生效单价与来源，供前端标注「协议价 / 默认价」）。
/// </summary>
public sealed class EffectivePriceDto
{
    /// <summary>商品 ID</summary>
    public string ProductId { get; init; } = string.Empty;

    /// <summary>生效单价（协议价优先，未配置时为商品销售价）</summary>
    public decimal UnitPrice { get; init; }

    /// <summary>单价来源（0 协议价 / 1 默认价）</summary>
    public int Source { get; init; }
}