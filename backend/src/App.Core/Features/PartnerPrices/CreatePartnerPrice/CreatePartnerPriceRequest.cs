using App.Core.Abstractions;

namespace App.Core.Features.PartnerPrices.CreatePartnerPrice;

/// <summary>
/// 新增客户协议价请求（客户 × 商品唯一）
/// </summary>
public sealed class CreatePartnerPriceRequest : IRequest<PartnerPriceDetailDto>
{
    /// <summary>客户 id</summary>
    public Guid PartnerId { get; init; }

    /// <summary>商品 id</summary>
    public Guid ProductId { get; init; }

    /// <summary>协议单价（0 ~ 9999999.99）</summary>
    public decimal Price { get; init; }

    /// <summary>备注，可空（≤ 200 字符）</summary>
    public string? Remark { get; init; }
}