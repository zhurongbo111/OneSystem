using App.Core.Abstractions;

namespace App.Core.Features.PartnerPrices.GetEffectivePrices;

/// <summary>
/// 批量取生效价请求（销售开单：选中客户后按商品批量取价，一次请求覆盖全部明细行）
/// </summary>
public sealed class GetEffectivePricesRequest : IRequest<IReadOnlyList<EffectivePriceDto>>
{
    /// <summary>客户 id</summary>
    public Guid PartnerId { get; init; }

    /// <summary>商品 id 集合（去重后 1 ~ 100 项）</summary>
    public IReadOnlyList<Guid> ProductIds { get; init; } = Array.Empty<Guid>();
}