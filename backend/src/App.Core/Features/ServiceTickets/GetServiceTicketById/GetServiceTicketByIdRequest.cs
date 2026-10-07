using App.Core.Abstractions;

namespace App.Core.Features.ServiceTickets.GetServiceTicketById;

/// <summary>
/// 服务工单详情查询请求（只读）
/// </summary>
public sealed class GetServiceTicketByIdRequest : IRequest<ServiceTicketDetailDto>
{
    /// <summary>工单 id</summary>
    public required Guid Id { get; init; }
}
