using App.Core.Abstractions;

namespace App.Core.Features.ServiceTickets.AssignServiceTicket;

/// <summary>
/// 指派服务工单负责人请求（design.md §3.3 / §3.4）：
/// 已关闭工单不可指派（40172）；负责人必须为存在的员工（40400）。取消指派可经编辑工单清空负责人。
/// </summary>
public sealed class AssignServiceTicketRequest : IRequest<ServiceTicketDetailDto>
{
    /// <summary>工单 id（由路由覆盖写入）</summary>
    public Guid Id { get; init; }

    /// <summary>负责人（员工）id（必填）</summary>
    public required Guid OwnerId { get; init; }
}
