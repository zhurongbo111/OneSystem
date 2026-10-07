using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.ServiceTickets.GetServiceTickets;

/// <summary>
/// 服务工单分页查询用例：仓储分页筛选（关键词 / 状态 / 优先级 / 负责人）→ 映射 DTO（含负责人姓名）
/// </summary>
public sealed class GetServiceTicketsRequestHandler : IRequestHandler<GetServiceTicketsRequest, PagedResult<ServiceTicketListItemDto>>
{
    private readonly IServiceTicketRepository _ticketRepository;

    /// <summary>
    /// 初始化服务工单分页查询用例处理器
    /// </summary>
    public GetServiceTicketsRequestHandler(IServiceTicketRepository ticketRepository)
    {
        _ticketRepository = ticketRepository;
    }

    /// <summary>
    /// 处理服务工单分页查询请求
    /// </summary>
    /// <param name="request">分页查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<ServiceTicketListItemDto>> HandleAsync(GetServiceTicketsRequest request, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _ticketRepository.GetPagedAsync(
            request.Keyword, request.Status, request.Priority, request.OwnerId,
            request.Page, request.PageSize, cancellationToken);

        return new PagedResult<ServiceTicketListItemDto>
        {
            Items = items.Select(ServiceTicketsDtoMapper.ToServiceTicketListItemDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
