using App.Core.Abstractions;

namespace App.Core.Features.PartnerPrices.DeletePartnerPrice;

/// <summary>
/// 删除客户协议价请求（删除即回退商品销售价；历史单据单价为快照，不受影响）
/// </summary>
public sealed class DeletePartnerPriceRequest : IRequest<object?>
{
    /// <summary>协议价 id（由控制器从路由注入）</summary>
    public Guid Id { get; set; }
}