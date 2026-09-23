using App.Core.Abstractions;

namespace App.Core.Features.PartnerPrices.GetEffectivePrices;

/// <summary>
/// 批量取生效价请求（销售开单：选中客户后按商品批量取价，一次请求覆盖全部明细行）
/// </summary>
public sealed class GetEffectivePricesRequest : IRequest<IReadOnlyList<EffectivePriceDto>>
{
    /// <summary>客户 id</summary>
    public Guid PartnerId { get; init; }

    /// <summary>
    /// 商品 id 集合（去重后 1 ~ 100 项）。
    /// 必须为具体集合类型：<c>IReadOnlyList&lt;T&gt;</c> 等接口类型无法被 query 集合绑定器实例化，
    /// 会静默绑定为空集合（表现为校验报「商品不能为空」）。
    /// </summary>
    public List<Guid> ProductIds { get; init; } = [];
}