using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Opportunities.CreateOpportunity;

/// <summary>
/// 新增商机请求（售前意向：不触碰库存与资金；客户可空）
/// </summary>
public sealed class CreateOpportunityRequest : IRequest<OpportunityDetailDto>
{
    /// <summary>商机名称（必填，1–50）</summary>
    public required string Name { get; init; }

    /// <summary>来源线索 id，可空（手工新建商机无来源线索）</summary>
    public Guid? LeadId { get; init; }

    /// <summary>关联客户 id，可空（线索阶段可能尚无正式客户档案）</summary>
    public Guid? PartnerId { get; init; }

    /// <summary>预计金额（≥ 0）</summary>
    public decimal Amount { get; init; }

    /// <summary>商机阶段（默认初步接洽）</summary>
    public OpportunityStage Stage { get; init; } = OpportunityStage.Initial;

    /// <summary>预计成交日期，可空</summary>
    public DateOnly? ExpectedCloseDate { get; init; }

    /// <summary>负责人（员工）id，可空</summary>
    public Guid? OwnerId { get; init; }

    /// <summary>备注，可空（≤ 200）</summary>
    public string? Remark { get; init; }
}
