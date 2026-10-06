namespace App.Core.Entities;

/// <summary>
/// 审批规则实体（对应 PostgreSQL 表 ApprovalRules，specs/042-erp-approval/design.md §2.1）。
/// 按**单据类型**一条规则：单据金额 ≥ <see cref="ThresholdAmount"/> 且 <see cref="Enabled"/> 时触发审批。
/// 规则默认不启用、迁移不预置数据：升级后既有单据行为逐条不变（由管理员按需开启）。
/// </summary>
public sealed class ApprovalRule
{
    /// <summary>规则 ID</summary>
    public Guid Id { get; set; }

    /// <summary>单据类型（复用 <see cref="SettlementOrderType"/>，语义已泛化为「业务单据类型」；唯一索引）</summary>
    public SettlementOrderType OrderType { get; set; }

    /// <summary>审批阈值（&gt; 0）</summary>
    public decimal ThresholdAmount { get; set; }

    /// <summary>是否启用（默认 false = 该类单据保存即生效）</summary>
    public bool Enabled { get; set; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>更新时间</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>创建人用户 id</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>更新人用户 id</summary>
    public Guid? UpdatedBy { get; set; }
}
