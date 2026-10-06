using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Leads;

/// <summary>
/// 线索实体 / 读模型 → 出参映射（禁止把实体暴露到 API；派生字段在 Mapper 内计算）。
/// </summary>
internal static class LeadsDtoMapper
{
    /// <summary>
    /// 线索实体 + 负责人姓名转详情 DTO
    /// </summary>
    public static LeadDetailDto ToLeadDetailDto(LeadDetail detail)
        => new()
        {
            Id = detail.Lead.Id.ToString(),
            LeadNo = detail.Lead.LeadNo,
            Name = detail.Lead.Name,
            Contact = detail.Lead.Contact,
            Phone = detail.Lead.Phone,
            Source = (int)detail.Lead.Source,
            Status = (int)detail.Lead.Status,
            OwnerId = detail.Lead.OwnerId?.ToString(),
            OwnerName = detail.OwnerName,
            OpportunityId = detail.Lead.OpportunityId?.ToString(),
            OpportunityNo = detail.Lead.OpportunityNo,
            Remark = detail.Lead.Remark,
            CreatedBy = detail.Lead.CreatedBy?.ToString(),
            CreatedAt = detail.Lead.CreatedAt,
            UpdatedAt = detail.Lead.UpdatedAt,
        };

    /// <summary>
    /// 列表行读模型转列表 DTO
    /// </summary>
    public static LeadListItemDto ToLeadListItemDto(LeadListItem item)
        => new()
        {
            Id = item.Lead.Id.ToString(),
            LeadNo = item.Lead.LeadNo,
            Name = item.Lead.Name,
            Contact = item.Lead.Contact,
            Phone = item.Lead.Phone,
            Source = (int)item.Lead.Source,
            Status = (int)item.Lead.Status,
            OwnerId = item.Lead.OwnerId?.ToString(),
            OwnerName = item.OwnerName,
            OpportunityNo = item.Lead.OpportunityNo,
            CreatedAt = item.Lead.CreatedAt,
        };

    /// <summary>
    /// 转商机结果（商机实体）转转商机出参
    /// </summary>
    public static ConvertLeadResultDto ToConvertLeadResultDto(Opportunity opportunity)
        => new()
        {
            OpportunityId = opportunity.Id.ToString(),
            OpportunityNo = opportunity.OpportunityNo,
        };
}
