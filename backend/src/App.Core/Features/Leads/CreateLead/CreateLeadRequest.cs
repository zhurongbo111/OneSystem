using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Leads.CreateLead;

/// <summary>
/// 新增线索请求（售前意向：不触碰库存与资金）
/// </summary>
public sealed class CreateLeadRequest : IRequest<LeadDetailDto>
{
    /// <summary>线索名称 / 公司（必填，1–50）</summary>
    public required string Name { get; init; }

    /// <summary>联系人，可空（≤ 30）</summary>
    public string? Contact { get; init; }

    /// <summary>联系电话，可空（≤ 20）</summary>
    public string? Phone { get; init; }

    /// <summary>线索来源（默认其他）</summary>
    public LeadSource Source { get; init; } = LeadSource.Other;

    /// <summary>线索状态（默认新线索；新建只允许新线索 / 跟进中，已转化只能由转商机产生）</summary>
    public LeadStatus Status { get; init; } = LeadStatus.New;

    /// <summary>负责人（员工）id，可空</summary>
    public Guid? OwnerId { get; init; }

    /// <summary>备注，可空（≤ 200）</summary>
    public string? Remark { get; init; }
}
