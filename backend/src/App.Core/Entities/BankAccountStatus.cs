namespace App.Core.Entities;

/// <summary>
/// 资金账户状态（specs/034-erp-cash/design.md §0.1）：停用账户不可被新收付款单引用，历史数据保留。
/// </summary>
public enum BankAccountStatus
{
    /// <summary>停用</summary>
    Disabled = 0,

    /// <summary>启用</summary>
    Enabled = 1,
}
