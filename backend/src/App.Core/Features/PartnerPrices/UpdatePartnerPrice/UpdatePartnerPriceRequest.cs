using App.Core.Abstractions;

namespace App.Core.Features.PartnerPrices.UpdatePartnerPrice;

/// <summary>
/// 编辑客户协议价请求（客户与商品不可改，请求体不含 partnerId / productId）
/// </summary>
public sealed class UpdatePartnerPriceRequest : IRequest<PartnerPriceDetailDto>
{
    /// <summary>协议价 id（由控制器从路由注入；set 供控制器赋值，不参与模型绑定）</summary>
    public Guid Id { get; set; }

    /// <summary>协议单价（0 ~ 9999999.99）</summary>
    public decimal Price { get; init; }

    /// <summary>备注，可空（≤ 200 字符；全量覆盖，空串视为清空）</summary>
    public string? Remark { get; init; }
}