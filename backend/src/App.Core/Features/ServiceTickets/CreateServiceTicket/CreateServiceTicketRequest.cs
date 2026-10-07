using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.ServiceTickets.CreateServiceTicket;

/// <summary>
/// 登记服务工单请求（售后留痕：不触碰库存与资金，specs/045-erp-crm-service design.md §3.3）
/// </summary>
public sealed class CreateServiceTicketRequest : IRequest<ServiceTicketDetailDto>
{
    /// <summary>客户 id（必填）</summary>
    public required Guid PartnerId { get; init; }

    /// <summary>联系人，可空（≤ 30）</summary>
    public string? Contact { get; init; }

    /// <summary>联系电话，可空（≤ 20）</summary>
    public string? Phone { get; init; }

    /// <summary>工单标题（必填，1–50）</summary>
    public required string Title { get; init; }

    /// <summary>问题描述，可空（≤ 500）</summary>
    public string? Description { get; init; }

    /// <summary>优先级（默认中）</summary>
    public TicketPriority Priority { get; init; } = TicketPriority.Medium;

    /// <summary>负责人（员工）id，可空</summary>
    public Guid? OwnerId { get; init; }

    /// <summary>备注，可空（≤ 200）</summary>
    public string? Remark { get; init; }
}
