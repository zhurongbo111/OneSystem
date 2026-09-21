using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.SalesShipments.GetSalesShipmentById;

/// <summary>
/// 销售单详情查询用例：不存在 → 40400；快照字段原样返回
/// </summary>
public sealed class GetSalesShipmentByIdRequestHandler : IRequestHandler<GetSalesShipmentByIdRequest, SalesShipmentDetailDto>
{
    private readonly ISalesShipmentRepository _salesShipmentRepository;

    /// <summary>
    /// 初始化销售单详情查询用例处理器
    /// </summary>
    public GetSalesShipmentByIdRequestHandler(ISalesShipmentRepository salesShipmentRepository)
    {
        _salesShipmentRepository = salesShipmentRepository;
    }

    /// <summary>
    /// 处理销售单详情查询请求
    /// </summary>
    /// <param name="request">详情查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<SalesShipmentDetailDto> HandleAsync(GetSalesShipmentByIdRequest request, CancellationToken cancellationToken = default)
    {
        var (order, items) = await _salesShipmentRepository.GetDetailAsync(request.Id, cancellationToken: cancellationToken);
        if (order is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "销售单不存在");
        }

        return SalesShipmentsDtoMapper.ToSalesShipmentDetailDto(order, items);
    }
}
