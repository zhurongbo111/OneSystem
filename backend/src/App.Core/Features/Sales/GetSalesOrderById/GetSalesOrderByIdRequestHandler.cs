using App.Core.Abstractions;
using App.Core.Features.Sales;
using App.Core.Errors;

namespace App.Core.Features.Sales.GetSalesOrderById;

/// <summary>
/// 销售单详情查询用例：不存在 → 40400；快照字段原样返回
/// </summary>
public sealed class GetSalesOrderByIdRequestHandler : IRequestHandler<GetSalesOrderByIdRequest, SalesOrderDetailDto>
{
    private readonly ISalesOrderRepository _salesOrderRepository;

    /// <summary>
    /// 初始化销售单详情查询用例处理器
    /// </summary>
    public GetSalesOrderByIdRequestHandler(ISalesOrderRepository salesOrderRepository)
    {
        _salesOrderRepository = salesOrderRepository;
    }

    /// <summary>
    /// 处理销售单详情查询请求
    /// </summary>
    /// <param name="request">详情查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<SalesOrderDetailDto> HandleAsync(GetSalesOrderByIdRequest request, CancellationToken cancellationToken = default)
    {
        var (order, items) = await _salesOrderRepository.GetDetailAsync(request.Id, cancellationToken);
        if (order is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "销售单不存在");
        }

        return SalesDtoMapper.ToSalesOrderDetailDto(order, items);
    }
}
