namespace App.Core.Entities;

/// <summary>
/// 站内信类型枚举（specs/041-erp-stock-alert/design.md §2.1）。
/// 0–2 为库存告警三类，与 <see cref="AlertType"/> 取值一致（去重台账只关心这几类）；
/// 3 / 4 为单据审批（`042`）的待审批 / 审批结果，5 为逾期应收预留值（追加枚举值不涉及迁移）。
/// </summary>
public enum NotificationType
{
    /// <summary>低库存（商品 × 仓汇总低于仓级安全库存）</summary>
    LowStock = 0,

    /// <summary>批次近效期（到期日在近效期窗口内且未过期）</summary>
    ExpiringBatch = 1,

    /// <summary>批次已过期（到期日已过且仍有库存）</summary>
    ExpiredBatch = 2,

    /// <summary>待审批（042：单据命中审批阈值，提醒具备审批权限的用户）</summary>
    ApprovalPending = 3,

    /// <summary>审批结果（042：审批通过 / 驳回 / 撤回，通知提交人）</summary>
    ApprovalDecided = 4,

    /// <summary>逾期应收（预留，催收提醒规则后续评估）</summary>
    ReceivableOverdue = 5,
}
