namespace App.Core.Entities;

/// <summary>
/// 会计期间状态（specs/033-erp-general-ledger/design.md §0.1）：
/// 已结账期间禁止新增 / 作废凭证，须先反结账
/// </summary>
public enum PeriodStatus
{
    /// <summary>未结账（开放记账）</summary>
    Open = 0,

    /// <summary>已结账</summary>
    Closed = 1,
}
