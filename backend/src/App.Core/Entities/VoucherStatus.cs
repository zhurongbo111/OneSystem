namespace App.Core.Entities;

/// <summary>
/// 记账凭证状态（specs/033-erp-general-ledger/design.md §0.1）：
/// 凭证生成 / 录入即过账，仅支持作废；只有 <see cref="Posted"/> 计入余额与报表
/// </summary>
public enum VoucherStatus
{
    /// <summary>已作废</summary>
    Voided = 0,

    /// <summary>已过账</summary>
    Posted = 1,
}
