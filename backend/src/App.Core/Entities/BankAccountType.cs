namespace App.Core.Entities;

/// <summary>
/// 资金账户类型（specs/034-erp-cash/design.md §0.1）：
/// 与收付款结算方式（`SettlementMethod`）强匹配——现金 ↔ <see cref="Cash"/>、银行转账 ↔ <see cref="Bank"/>。
/// </summary>
public enum BankAccountType
{
    /// <summary>现金账户</summary>
    Cash = 1,

    /// <summary>银行账户</summary>
    Bank = 2,
}
