namespace App.Core.Entities;

/// <summary>
/// 收付款方式（specs/023-erp-settlement/design.md §2.1）
/// </summary>
public enum SettlementMethod
{
    /// <summary>现金</summary>
    Cash = 0,

    /// <summary>银行转账</summary>
    BankTransfer = 1,

    /// <summary>其他</summary>
    Other = 2,
}
