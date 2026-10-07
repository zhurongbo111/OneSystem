namespace App.Core.Features.ServiceTickets;

/// <summary>
/// 服务工单出参共享模型（与前端 DTO camelCase 一一对应）。
/// 枚举回整数值，文案与颜色映射见 specs/045-erp-crm-service design.md §0.1（前端映射）。
/// </summary>
public sealed class ServiceTicketDetailDto
{
    /// <summary>工单 id</summary>
    public required string Id { get; init; }

    /// <summary>工单号（SV + yyyyMMdd + 4 位序号）</summary>
    public required string TicketNo { get; init; }

    /// <summary>客户 id</summary>
    public required string PartnerId { get; init; }

    /// <summary>客户名称快照</summary>
    public required string PartnerName { get; init; }

    /// <summary>联系人，可空</summary>
    public string? Contact { get; init; }

    /// <summary>联系电话，可空</summary>
    public string? Phone { get; init; }

    /// <summary>工单标题</summary>
    public required string Title { get; init; }

    /// <summary>问题描述，可空</summary>
    public string? Description { get; init; }

    /// <summary>优先级（0 低 / 1 中 / 2 高）</summary>
    public required int Priority { get; init; }

    /// <summary>状态（0 待处理 / 1 处理中 / 2 已解决 / 3 已关闭）</summary>
    public required int Status { get; init; }

    /// <summary>负责人（员工）id，可空</summary>
    public string? OwnerId { get; init; }

    /// <summary>负责人姓名，未指派时为空</summary>
    public string? OwnerName { get; init; }

    /// <summary>解决时间，可空（置「已解决」时记录，重开时清空）</summary>
    public DateTimeOffset? ResolvedAt { get; init; }

    /// <summary>备注，可空</summary>
    public string? Remark { get; init; }

    /// <summary>创建人用户 id</summary>
    public string? CreatedBy { get; init; }

    /// <summary>创建时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>更新时间</summary>
    public required DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>服务工单列表行出参模型</summary>
public sealed class ServiceTicketListItemDto
{
    /// <summary>工单 id</summary>
    public required string Id { get; init; }

    /// <summary>工单号</summary>
    public required string TicketNo { get; init; }

    /// <summary>客户 id</summary>
    public required string PartnerId { get; init; }

    /// <summary>客户名称快照</summary>
    public required string PartnerName { get; init; }

    /// <summary>工单标题</summary>
    public required string Title { get; init; }

    /// <summary>优先级</summary>
    public required int Priority { get; init; }

    /// <summary>状态</summary>
    public required int Status { get; init; }

    /// <summary>负责人（员工）id，可空</summary>
    public string? OwnerId { get; init; }

    /// <summary>负责人姓名，未指派时为空</summary>
    public string? OwnerName { get; init; }

    /// <summary>解决时间，可空</summary>
    public DateTimeOffset? ResolvedAt { get; init; }

    /// <summary>创建时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
