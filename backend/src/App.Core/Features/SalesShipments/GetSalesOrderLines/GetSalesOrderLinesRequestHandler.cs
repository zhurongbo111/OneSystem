using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.SalesShipments.GetSalesOrderLines;

/// <summary>
/// 关联订单明细查询用例：订单不存在 → 40400；返回订单头与明细（含未发数量），供出库开单页带出
/// </summary>
public sealed class GetSalesOrderLinesRequestHandler : IRequestHandler<GetSalesOrderLinesRequest, SalesOrderLinesDto>
{
    private readonly ISalesOrderRepository _salesOrderRepository;

    /// <summary>
    /// 初始化关联订单明细查询用例处理器
    /// </summary>
    public GetSalesOrderLinesRequestHandler(ISalesOrderRepository salesOrderRepository)
    {
        _salesOrderRepository = salesOrderRepository;
    }

    /// <summary>
    /// 处理关联订单明细查询请求
    /// </summary>
    /// <param name="request">查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<SalesOrderLinesDto> HandleAsync(GetSalesOrderLinesRequest request, CancellationToken cancellationToken = default)
    {
        var (order, items) = await _salesOrderRepository.GetLinesAsync(request.OrderId, cancellationToken);
        if (order is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "销售订单不存在");
        }

        return SalesShipmentsDtoMapper.ToSalesOrderLinesDto(order, items);
    }
}
