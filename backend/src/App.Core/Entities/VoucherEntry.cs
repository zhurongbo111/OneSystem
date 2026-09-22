namespace App.Core.Entities;

/// <summary>
/// 凭证分录实体（对应 PostgreSQL 表 VoucherEntries，specs/033-erp-general-ledger/design.md §2.3）。
/// 每行借方与贷方恰有一个大于 0（另一为 0）；科目为末级启用科目，编码 / 名称为分录快照。
/// </summary>
public sealed class VoucherEntry
{
    /// <summary>分录 ID</summary>
    public Guid Id { get; set; }

    /// <summary>所属凭证 id（外键 → Vouchers(Id)）</summary>
    public Guid VoucherId { get; set; }

    /// <summary>行号（1 起）</summary>
    public int LineNo { get; set; }

    /// <summary>科目 id（外键 → Accounts(Id)；须为末级启用科目）</summary>
    public Guid AccountId { get; set; }

    /// <summary>科目编码快照</summary>
    public string AccountCode { get; set; } = string.Empty;

    /// <summary>科目名称快照</summary>
    public string AccountName { get; set; } = string.Empty;

    /// <summary>行摘要，可空</summary>
    public string? Summary { get; set; }

    /// <summary>借方金额（与 <see cref="Credit"/> 恰有一个大于 0）</summary>
    public decimal Debit { get; set; }

    /// <summary>贷方金额</summary>
    public decimal Credit { get; set; }
}
