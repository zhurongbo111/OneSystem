using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Leads.UpdateLeadStatus;

/// <summary>
/// 线索状态流转请求（design.md §3.3 / §3.4）：终态（已转化 / 已废弃）不可再改；
/// 「已转化」只能由转商机产生（<see cref="LeadStatusRules"/>）。
/// </summary>
public sealed class UpdateLeadStatusRequest : IRequest<LeadDetailDto>
{
    /// <summary>线索 id（由路由覆盖写入）</summary>
    public Guid Id { get; init; }

    /// <summary>目标状态（新线索 / 跟进中 / 已废弃；已转化不允许直接设置）</summary>
    public LeadStatus Status { get; init; } = LeadStatus.New;
}
