namespace App.Core.Entities;

/// <summary>
/// 告警信号类型枚举（specs/041-erp-stock-alert/design.md §2.2）：仅用于当日去重台账 <see cref="AlertRecord"/>，
/// 取值与 <see cref="NotificationType"/> 的前三类一致（台账只关心「信号」本身，与通知渠道解耦）。
/// </summary>
public enum AlertType
{
    /// <summary>低库存</summary>
    LowStock = 0,

    /// <summary>批次近效期</summary>
    ExpiringBatch = 1,

    /// <summary>批次已过期</summary>
    ExpiredBatch = 2,
}
