using App.Core.Abstractions;

namespace App.Core.Features.SalesShipments.GetSalesShipmentById;

/// <summary>
/// 销售单详情查询请求（只读；纯参数用例，无格式校验器）
/// </summary>
public sealed class GetSalesShipmentByIdRequest : IRequest<SalesShipmentDetailDto>
{
    /// <summary>销售单 id</summary>
    public required Guid Id { get; init; }
}
