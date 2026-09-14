using App.Core.Entities;

namespace App.Core.Features.Partners;

/// <summary>
/// 往来单位实体 → 出参模型 的映射（集中一处，避免各用例重复拼装）
/// </summary>
internal static class PartnerDtoMapper
{
    /// <summary>映射出参（列表 / 详情 / 新增 / 编辑 / 启停共用同一模型）</summary>
    public static PartnerDto ToPartnerDto(Partner partner)
        => new()
        {
            Id = partner.Id.ToString(),
            Name = partner.Name,
            Type = (int)partner.Type,
            Contact = partner.Contact,
            Phone = partner.Phone,
            Address = partner.Address,
            Remark = partner.Remark,
            Status = (int)partner.Status,
            CreatedAt = partner.CreatedAt,
            UpdatedAt = partner.UpdatedAt,
        };
}
