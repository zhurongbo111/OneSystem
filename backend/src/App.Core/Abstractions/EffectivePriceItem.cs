using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 生效价读模型（仓储出参契约，批量取价结果；不暴露到 API）。
/// </summary>
public sealed record EffectivePriceItem
{
    /// <summary>商品 id</summary>
    public required Guid ProductId { get; init; }

    /// <summary>生效单价（有协议价取协议价，否则取商品销售价）</summary>
    public required decimal UnitPrice { get; init; }

    /// <summary>单价来源</summary>
    public required PriceSource Source { get; init; }
}