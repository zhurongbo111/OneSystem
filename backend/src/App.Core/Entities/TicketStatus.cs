namespace App.Core.Entities;

/// <summary>
/// 服务工单状态（specs/045-erp-crm-service design.md §0.1，取值与文案为该规格唯一事实源）。
/// 允许流转：待处理 → 处理中 / 已解决 / 已关闭；处理中 → 已解决 / 已关闭；已解决 → 已关闭 / 处理中（重开）；
/// <see cref="Closed"/> 为终态（不可编辑、不可改状态，否则 40172）。
/// </summary>
public enum TicketStatus
{
    /// <summary>待处理</summary>
    Pending = 0,

    /// <summary>处理中</summary>
    Processing = 1,

    /// <summary>已解决（记录解决时间）</summary>
    Resolved = 2,

    /// <summary>已关闭（终态）</summary>
    Closed = 3,
}
