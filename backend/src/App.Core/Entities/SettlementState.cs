namespace App.Core.Entities;

/// <summary>
/// 单据结算状态（展示用，由 <c>SettledAmount</c> 与 <c>TotalAmount</c> 推导，不落列；
/// specs/023-erp-settlement/design.md §0）。取代原 <c>OrderSettlementStatus</c>。
/// </summary>
public enum SettlementState
{
    /// <summary>未结算（SettledAmount ≤ 0）</summary>
    Unsettled = 0,

    /// <summary>部分结算（0 &lt; SettledAmount &lt; TotalAmount）</summary>
    PartiallySettled = 1,

    /// <summary>已结算（SettledAmount ≥ TotalAmount）</summary>
    Settled = 2,
}
