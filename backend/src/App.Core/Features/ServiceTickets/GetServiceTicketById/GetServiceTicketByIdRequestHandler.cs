using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.ServiceTickets.GetServiceTicketById;

/// <summary>
/// 服务工单详情查询用例：取工单（含负责人姓名），不存在报 40400
/// </summary>
public sealed class GetServiceTicketByIdRequestHandler : IRequestHandler<GetServiceTicketByIdRequest, ServiceTicketDetailDto>
{
    private readonly IServiceTicketRepository _ticketRepository;

    /// <summary>
    /// 初始化服务工单详情查询用例处理器
    /// </summary>
    public GetServiceTicketByIdRequestHandler(IServiceTicketRepository ticketRepository)
    {
        _ticketRepository = ticketRepository;
    }

    /// <summary>
    /// 处理服务工单详情查询请求
    /// </summary>
    /// <param name="request">详情查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ServiceTicketDetailDto> HandleAsync(GetServiceTicketByIdRequest request, CancellationToken cancellationToken = default)
    {
        var detail = await _ticketRepository.GetByIdAsync(request.Id, cancellationToken);
        if (detail is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "工单不存在");
        }

        return ServiceTicketsDtoMapper.ToServiceTicketDetailDto(detail);
    }
}
