using App.Core.Abstractions;

namespace App.Core.Features.PartnerPrices.GetPartnerPriceById;

/// <summary>
/// 客户协议价详情查询请求
/// </summary>
public sealed class GetPartnerPriceByIdRequest : IRequest<PartnerPriceDetailDto>
{
    /// <summary>协议价 id（由控制器从路由注入）</summary>
    public Guid Id { get; set; }
}