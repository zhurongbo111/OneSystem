using App.Core.Abstractions;

namespace App.Core.Features.ServiceTickets;

/// <summary>
/// 服务工单读模型 → 出参映射（禁止把实体暴露到 API；派生字段在 Mapper 内计算）。
/// </summary>
internal static class ServiceTicketsDtoMapper
{
    /// <summary>
    /// 工单详情读模型转详情 DTO
    /// </summary>
    public static ServiceTicketDetailDto ToServiceTicketDetailDto(ServiceTicketDetail detail)
        => new()
        {
            Id = detail.Ticket.Id.ToString(),
            TicketNo = detail.Ticket.TicketNo,
            PartnerId = detail.Ticket.PartnerId.ToString(),
            PartnerName = detail.Ticket.PartnerName,
            Contact = detail.Ticket.Contact,
            Phone = detail.Ticket.Phone,
            Title = detail.Ticket.Title,
            Description = detail.Ticket.Description,
            Priority = (int)detail.Ticket.Priority,
            Status = (int)detail.Ticket.Status,
            OwnerId = detail.Ticket.OwnerId?.ToString(),
            OwnerName = detail.OwnerName,
            ResolvedAt = detail.Ticket.ResolvedAt,
            Remark = detail.Ticket.Remark,
            CreatedBy = detail.Ticket.CreatedBy?.ToString(),
            CreatedAt = detail.Ticket.CreatedAt,
            UpdatedAt = detail.Ticket.UpdatedAt,
        };

    /// <summary>
    /// 列表行读模型转列表 DTO
    /// </summary>
    public static ServiceTicketListItemDto ToServiceTicketListItemDto(ServiceTicketListItem item)
        => new()
        {
            Id = item.Ticket.Id.ToString(),
            TicketNo = item.Ticket.TicketNo,
            PartnerId = item.Ticket.PartnerId.ToString(),
            PartnerName = item.Ticket.PartnerName,
            Title = item.Ticket.Title,
            Priority = (int)item.Ticket.Priority,
            Status = (int)item.Ticket.Status,
            OwnerId = item.Ticket.OwnerId?.ToString(),
            OwnerName = item.OwnerName,
            ResolvedAt = item.Ticket.ResolvedAt,
            CreatedAt = item.Ticket.CreatedAt,
        };
}
