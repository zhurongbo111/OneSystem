using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.ServiceTickets.UpdateServiceTicket;

/// <summary>
/// 编辑服务工单请求（**全量覆盖**语义，AGENTS.md §4.5）：可空字段缺省 / 空白即清空；
/// 单号不可改（不在请求中）；状态只能经 <c>PUT /api/service-tickets/{id}/status</c> 推进（也不在请求中）。
/// </summary>
public sealed class UpdateServiceTicketRequest : IRequest<ServiceTicketDetailDto>
{
    /// <summary>工单 id（由路由覆盖写入；请求体可不传）</summary>
    public Guid Id { get; init; }

    /// <summary>客户 id（必填；可改，客户名称快照随之刷新）</summary>
    public required Guid PartnerId { get; init; }

    /// <summary>联系人，可空（≤ 30）</summary>
    public string? Contact { get; init; }

    /// <summary>联系电话，可空（≤ 20）</summary>
    public string? Phone { get; init; }

    /// <summary>工单标题（必填，1–50；可改）</summary>
    public required string Title { get; init; }

    /// <summary>问题描述，可空（≤ 500）</summary>
    public string? Description { get; init; }

    /// <summary>优先级</summary>
    public TicketPriority Priority { get; init; } = TicketPriority.Medium;

    /// <summary>负责人（员工）id，可空</summary>
    public Guid? OwnerId { get; init; }

    /// <summary>备注，可空（≤ 200）</summary>
    public string? Remark { get; init; }
}
