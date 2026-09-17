using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.SalesShipments.GetSalesShipments;

/// <summary>
/// 销售单分页查询用例：仓储分页筛选（含作废单据）→ 映射 DTO（含 Status 供前端置灰）
/// </summary>
public sealed class GetSalesShipmentsRequestHandler : IRequestHandler<GetSalesShipmentsRequest, PagedResult<SalesShipmentListItemDto>>
{
    private readonly ISalesShipmentRepository _salesShipmentRepository;

    /// <summary>
    /// 初始化销售单分页查询用例处理器
    /// </summary>
    public GetSalesShipmentsRequestHandler(ISalesShipmentRepository salesShipmentRepository)
    {
        _salesShipmentRepository = salesShipmentRepository;
    }

    /// <summary>
    /// 处理销售单分页查询请求
    /// </summary>
    /// <param name="request">分页查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<SalesShipmentListItemDto>> HandleAsync(GetSalesShipmentsRequest request, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _salesShipmentRepository.GetPagedAsync(
            request.Keyword, request.PartnerId, request.OrderId, request.Start, request.End, request.SettlementState,
            request.Page, request.PageSize, cancellationToken);

        return new PagedResult<SalesShipmentListItemDto>
        {
            Items = items.Select(SalesShipmentsDtoMapper.ToSalesShipmentListItemDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
