using App.Core.Abstractions;

namespace App.Core.Features.SalesShipments.GetSalesOrderPicks;

/// <summary>
/// 可关联销售订单候选查询用例：按客户取待发货 / 部分发货订单（创建时间倒序），供出库开单页下拉
/// </summary>
public sealed class GetSalesOrderPicksRequestHandler : IRequestHandler<GetSalesOrderPicksRequest, IReadOnlyList<SalesOrderPickDto>>
{
    private readonly ISalesOrderRepository _salesOrderRepository;

    /// <summary>
    /// 初始化可关联订单候选查询用例处理器
    /// </summary>
    public GetSalesOrderPicksRequestHandler(ISalesOrderRepository salesOrderRepository)
    {
        _salesOrderRepository = salesOrderRepository;
    }

    /// <summary>
    /// 处理可关联订单候选查询请求
    /// </summary>
    /// <param name="request">查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<IReadOnlyList<SalesOrderPickDto>> HandleAsync(GetSalesOrderPicksRequest request, CancellationToken cancellationToken = default)
    {
        var picks = await _salesOrderRepository.GetPicksAsync(request.PartnerId, cancellationToken);
        return picks.Select(SalesShipmentsDtoMapper.ToSalesOrderPickDto).ToList();
    }
}
