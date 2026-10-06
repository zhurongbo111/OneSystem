using App.Core.Abstractions;

namespace App.Core.Features.Opportunities;

/// <summary>
/// 商机实体 / 读模型 → 出参映射（禁止把实体暴露到 API；派生字段在 Mapper 内计算）。
/// </summary>
internal static class OpportunitiesDtoMapper
{
    /// <summary>
    /// 商机实体 + 负责人姓名转详情 DTO
    /// </summary>
    public static OpportunityDetailDto ToOpportunityDetailDto(OpportunityDetail detail)
        => new()
        {
            Id = detail.Opportunity.Id.ToString(),
            OpportunityNo = detail.Opportunity.OpportunityNo,
            Name = detail.Opportunity.Name,
            LeadId = detail.Opportunity.LeadId?.ToString(),
            PartnerId = detail.Opportunity.PartnerId?.ToString(),
            PartnerName = detail.Opportunity.PartnerName,
            Amount = detail.Opportunity.Amount,
            Stage = (int)detail.Opportunity.Stage,
            ExpectedCloseDate = detail.Opportunity.ExpectedCloseDate,
            OwnerId = detail.Opportunity.OwnerId?.ToString(),
            OwnerName = detail.OwnerName,
            Remark = detail.Opportunity.Remark,
            CreatedBy = detail.Opportunity.CreatedBy?.ToString(),
            CreatedAt = detail.Opportunity.CreatedAt,
            UpdatedAt = detail.Opportunity.UpdatedAt,
        };

    /// <summary>
    /// 列表行读模型转列表 DTO
    /// </summary>
    public static OpportunityListItemDto ToOpportunityListItemDto(OpportunityListItem item)
        => new()
        {
            Id = item.Opportunity.Id.ToString(),
            OpportunityNo = item.Opportunity.OpportunityNo,
            Name = item.Opportunity.Name,
            PartnerId = item.Opportunity.PartnerId?.ToString(),
            PartnerName = item.Opportunity.PartnerName,
            Amount = item.Opportunity.Amount,
            Stage = (int)item.Opportunity.Stage,
            ExpectedCloseDate = item.Opportunity.ExpectedCloseDate,
            OwnerId = item.Opportunity.OwnerId?.ToString(),
            OwnerName = item.OwnerName,
            CreatedAt = item.Opportunity.CreatedAt,
        };
}
