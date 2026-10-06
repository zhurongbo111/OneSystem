namespace App.Core.Entities;

/// <summary>
/// 审批记录实体（对应 PostgreSQL 表 Approvals，specs/042-erp-approval/design.md §2.2）。
/// 一张单据一条记录（唯一索引 <c>(OrderType, OrderId)</c>）：驳回 / 撤回后不再重新提交
/// （四类单据不可编辑，要重做就作废重开新单，与「一步式」口径一致）。
/// 被审批单据的关键字段以**快照**落库（单号 / 往来名称 / 金额），列表与详情无需回查单据表。
/// </summary>
public sealed class Approval
{
    /// <summary>审批记录 ID</summary>
    public Guid Id { get; set; }

    /// <summary>单据类型（复用 <see cref="SettlementOrderType"/>）</summary>
    public SettlementOrderType OrderType { get; set; }

    /// <summary>被审批单据 id（唯一索引 <c>(OrderType, OrderId)</c>）</summary>
    public Guid OrderId { get; set; }

    /// <summary>单据号快照</summary>
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>往来单位名称快照</summary>
    public string PartnerName { get; set; } = string.Empty;

    /// <summary>单据金额快照（触发审批时的总额）</summary>
    public decimal Amount { get; set; }

    /// <summary>审批状态（记录不会为 <see cref="ApprovalStatus.None"/>，默认待审批）</summary>
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;

    /// <summary>提交人用户 id</summary>
    public Guid SubmittedBy { get; set; }

    /// <summary>提交时间</summary>
    public DateTimeOffset SubmittedAt { get; set; }

    /// <summary>审批人用户 id（未决定时为空）</summary>
    public Guid? DecidedBy { get; set; }

    /// <summary>审批时间（未决定时为空）</summary>
    public DateTimeOffset? DecidedAt { get; set; }

    /// <summary>审批意见（驳回必填，通过可空，≤ 200 字符）</summary>
    public string? DecisionRemark { get; set; }
}
