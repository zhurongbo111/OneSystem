namespace App.Core.Entities;

/// <summary>
/// 资金账户实体（对应 PostgreSQL 表 BankAccounts，specs/034-erp-cash/design.md §2.1）。
/// 资金账户是企业主数据（现金 / 银行存款），收付款单（`023`）通过 <c>BankAccountId</c> 关联；
/// 账户余额 = <see cref="InitialBalance"/> + Σ 收款 − Σ 付款（派生值，不落列）。
/// </summary>
public sealed class BankAccount
{
    /// <summary>资金账户 ID</summary>
    public Guid Id { get; set; }

    /// <summary>账户编码，全局唯一</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>账户名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>账户类型（1 现金 / 2 银行）</summary>
    public BankAccountType Type { get; set; }

    /// <summary>开户行（银行账户），现金账户为空</summary>
    public string? BankName { get; set; }

    /// <summary>银行账号（银行账户），现金账户为空</summary>
    public string? AccountNo { get; set; }

    /// <summary>初始余额（上线建账起点，≥ 0）</summary>
    public decimal InitialBalance { get; set; }

    /// <summary>账户状态（1 启用 / 0 停用）</summary>
    public BankAccountStatus Status { get; set; } = BankAccountStatus.Enabled;

    /// <summary>备注，可空</summary>
    public string? Remark { get; set; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>更新时间</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>创建人用户 id</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>更新人用户 id</summary>
    public Guid? UpdatedBy { get; set; }
}
