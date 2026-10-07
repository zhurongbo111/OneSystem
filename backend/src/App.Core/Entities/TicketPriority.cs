namespace App.Core.Entities;

/// <summary>
/// 服务工单优先级（specs/045-erp-crm-service design.md §0.1，取值与文案为该规格唯一事实源）。
/// </summary>
public enum TicketPriority
{
    /// <summary>低</summary>
    Low = 0,

    /// <summary>中（默认）</summary>
    Medium = 1,

    /// <summary>高</summary>
    High = 2,
}
