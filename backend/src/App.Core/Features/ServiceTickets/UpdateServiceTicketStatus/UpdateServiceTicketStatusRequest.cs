using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.ServiceTickets.UpdateServiceTicketStatus;

/// <summary>
/// 服务工单状态流转请求（design.md §0.1 / §3.3）：白名单外的流转（含已关闭为终态）一律 40172。
/// </summary>
public sealed class UpdateServiceTicketStatusRequest : IRequest<ServiceTicketDetailDto>
{
    /// <summary>工单 id（由路由覆盖写入）</summary>
    public Guid Id { get; init; }

    /// <summary>目标状态（待处理不可回退；按 §0.1 白名单校验）</summary>
    public TicketStatus Status { get; init; } = TicketStatus.Pending;
}
