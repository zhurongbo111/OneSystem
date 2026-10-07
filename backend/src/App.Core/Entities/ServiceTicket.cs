namespace App.Core.Entities;

/// <summary>
/// 服务工单实体（对应 PostgreSQL 表 ServiceTickets）。
/// **售后留痕记录**：不做删除（关闭即归档），不产生库存 / 资金影响（specs/045-erp-crm-service design.md §1）；
/// 客户关联 <see cref="Partner"/>（`013`，名称快照到 <see cref="PartnerName"/>），
/// 负责人关联 <see cref="Employee"/>（`030`），不感知系统账号。
/// </summary>
public sealed class ServiceTicket
{
    /// <summary>工单 ID</summary>
    public Guid Id { get; set; }

    /// <summary>工单号，唯一，后端生成（SV + yyyyMMdd + 4 位序号，如 SV202610070001）</summary>
    public string TicketNo { get; set; } = string.Empty;

    /// <summary>客户 id（外键 → Partners(Id)）</summary>
    public Guid PartnerId { get; set; }

    /// <summary>客户名称快照（创建 / 编辑时按客户主数据写入）</summary>
    public string PartnerName { get; set; } = string.Empty;

    /// <summary>联系人，可空</summary>
    public string? Contact { get; set; }

    /// <summary>联系电话，可空</summary>
    public string? Phone { get; set; }

    /// <summary>工单标题</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>问题描述，可空</summary>
    public string? Description { get; set; }

    /// <summary>优先级（默认中）</summary>
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;

    /// <summary>状态（默认待处理；已关闭为终态）</summary>
    public TicketStatus Status { get; set; } = TicketStatus.Pending;

    /// <summary>负责人（员工）id，可空（外键 → Employees(Id)）</summary>
    public Guid? OwnerId { get; set; }

    /// <summary>解决时间，可空（置「已解决」时记录首次；重开为处理中时清空）</summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>备注，可空</summary>
    public string? Remark { get; set; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>更新时间</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>创建人用户 id</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>更新人用户 id</summary>
    public Guid? UpdatedBy { get; set; }
}
