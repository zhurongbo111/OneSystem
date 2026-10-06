using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Approvals.UpdateApprovalRules;

/// <summary>
/// 审批规则保存请求（specs/042-erp-approval/design.md §3.4）：逐类型 upsert（阈值 + 启用开关）
/// </summary>
public sealed class UpdateApprovalRulesRequest : IRequest<IReadOnlyList<ApprovalRuleDto>>
{
    /// <summary>规则项（1–4 项，单据类型不得重复）</summary>
    public required IReadOnlyList<UpdateApprovalRuleItem> Rules { get; init; }
}

/// <summary>审批规则单项（单据类型 + 阈值 + 启用）</summary>
public sealed class UpdateApprovalRuleItem
{
    /// <summary>单据类型</summary>
    public SettlementOrderType OrderType { get; init; }

    /// <summary>审批阈值（&gt; 0 且 ≤ 9999999.99）</summary>
    public decimal ThresholdAmount { get; init; }

    /// <summary>是否启用</summary>
    public bool Enabled { get; init; }
}
