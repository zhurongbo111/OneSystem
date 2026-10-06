namespace App.Core.Entities;

/// <summary>
/// 单据审批状态（specs/042-erp-approval/design.md §0.1）。
/// 四类单据表与审批记录表共用同一枚举（审批记录不会取 <see cref="None"/>）。
/// 与 <see cref="OrderStatus"/> **正交**：审批可驳回并置单据作废，两者分列避免状态组合爆炸。
/// </summary>
public enum ApprovalStatus
{
    /// <summary>无需审批（默认；未命中阈值规则的单据）</summary>
    None = 0,

    /// <summary>待审批（命中规则，尚未生效）</summary>
    Pending = 1,

    /// <summary>已通过（审批通过并已生效）</summary>
    Approved = 2,

    /// <summary>已驳回（单据一并作废，从未生效）</summary>
    Rejected = 3,

    /// <summary>已撤回（提交人本人撤回，单据一并作废，从未生效）</summary>
    Withdrawn = 4,
}
