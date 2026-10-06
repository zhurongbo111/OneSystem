using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Leads.UpdateLead;

/// <summary>
/// 编辑线索请求（**全量覆盖**语义，AGENTS.md §4.5）：可空字段缺省 / 空白即清空；单号不可改（不在请求中）。
/// 状态变更与 <c>UpdateLeadStatus</c> 共用 <see cref="LeadStatusRules"/> 判据（终态拒绝、已转化只能由转商机产生）。
/// </summary>
public sealed class UpdateLeadRequest : IRequest<LeadDetailDto>
{
    /// <summary>线索 id（由路由覆盖写入；请求体可不传）</summary>
    public Guid Id { get; init; }

    /// <summary>线索名称 / 公司（必填，1–50；可改）</summary>
    public required string Name { get; init; }

    /// <summary>联系人，可空（≤ 30）</summary>
    public string? Contact { get; init; }

    /// <summary>联系电话，可空（≤ 20）</summary>
    public string? Phone { get; init; }

    /// <summary>线索来源</summary>
    public LeadSource Source { get; init; } = LeadSource.Other;

    /// <summary>线索状态（终态不可改；「已转化」只能由转商机产生）</summary>
    public LeadStatus Status { get; init; } = LeadStatus.New;

    /// <summary>负责人（员工）id，可空</summary>
    public Guid? OwnerId { get; init; }

    /// <summary>备注，可空（≤ 200）</summary>
    public string? Remark { get; init; }
}
