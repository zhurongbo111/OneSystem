using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Opportunities.UpdateOpportunity;

/// <summary>
/// 编辑商机请求（**全量覆盖**语义，AGENTS.md §4.5）：可空字段缺省 / 空白即清空；单号与来源线索不可改（不在请求中）。
/// 阶段变更与 <c>UpdateOpportunityStage</c> 共用 <see cref="OpportunityStageRules"/> 判据（终态拒绝，40169）。
/// </summary>
public sealed class UpdateOpportunityRequest : IRequest<OpportunityDetailDto>
{
    /// <summary>商机 id（由路由覆盖写入；请求体可不传）</summary>
    public Guid Id { get; init; }

    /// <summary>商机名称（必填，1–50；可改）</summary>
    public required string Name { get; init; }

    /// <summary>关联客户 id，可空（可改；改后客户名称快照同步刷新）</summary>
    public Guid? PartnerId { get; init; }

    /// <summary>预计金额（≥ 0）</summary>
    public decimal Amount { get; init; }

    /// <summary>商机阶段（终态不可改）</summary>
    public OpportunityStage Stage { get; init; } = OpportunityStage.Initial;

    /// <summary>预计成交日期，可空</summary>
    public DateOnly? ExpectedCloseDate { get; init; }

    /// <summary>负责人（员工）id，可空</summary>
    public Guid? OwnerId { get; init; }

    /// <summary>备注，可空（≤ 200）</summary>
    public string? Remark { get; init; }
}
