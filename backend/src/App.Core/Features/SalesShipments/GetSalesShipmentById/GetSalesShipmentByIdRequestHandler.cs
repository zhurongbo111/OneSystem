using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.SalesShipments.GetSalesShipmentById;

/// <summary>
/// 销售单详情查询用例：不存在 → 40400；快照字段原样返回（到期日由客户账期推导，`036` §0.3）
/// </summary>
public sealed class GetSalesShipmentByIdRequestHandler : IRequestHandler<GetSalesShipmentByIdRequest, SalesShipmentDetailDto>
{
    private readonly ISalesShipmentRepository _salesShipmentRepository;
    private readonly IPartnerRepository _partnerRepository;

    /// <summary>
    /// 初始化销售单详情查询用例处理器
    /// </summary>
    public GetSalesShipmentByIdRequestHandler(
        ISalesShipmentRepository salesShipmentRepository,
        IPartnerRepository partnerRepository)
    {
        _salesShipmentRepository = salesShipmentRepository;
        _partnerRepository = partnerRepository;
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

        // 到期日按客户当前账期推导（改账期会影响历史单据的到期日，规格 §5 已接受该语义）
        var partner = await _partnerRepository.GetByIdAsync(order.PartnerId, cancellationToken);
        return SalesShipmentsDtoMapper.ToSalesShipmentDetailDto(order, items, partner?.PaymentTermDays ?? 0);
    }
}
