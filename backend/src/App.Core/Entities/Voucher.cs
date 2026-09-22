namespace App.Core.Entities;

/// <summary>
/// 记账凭证实体（对应 PostgreSQL 表 Vouchers，specs/033-erp-general-ledger/design.md §2.2）。
/// 分录见 <see cref="VoucherEntry"/>；自动凭证由单据生效 / 作废时同事务生成 / 作废；
/// 借贷合计由后端按分录重算（不信任调用方传值）。
/// </summary>
public sealed class Voucher
{
    /// <summary>凭证 ID</summary>
    public Guid Id { get; set; }

    /// <summary>凭证号，唯一，后端生成（记-YYYYMM-0001）</summary>
    public string VoucherNo { get; set; } = string.Empty;

    /// <summary>记账日期</summary>
    public DateTimeOffset VoucherDate { get; set; }

    /// <summary>归属期间 id（外键 → AccountingPeriods(Id)）</summary>
    public Guid PeriodId { get; set; }

    /// <summary>摘要</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>来源类型（0 手工 / 1–7 单据自动凭证与成本结转）</summary>
    public VoucherSourceType SourceType { get; set; }

    /// <summary>来源单据 id（手工凭证为空）</summary>
    public Guid? SourceId { get; set; }

    /// <summary>来源单据号快照（手工凭证为空）</summary>
    public string? SourceNo { get; set; }

    /// <summary>借方合计 = Σ 分录借方</summary>
    public decimal TotalDebit { get; set; }

    /// <summary>贷方合计 = Σ 分录贷方（应等于 <see cref="TotalDebit"/>）</summary>
    public decimal TotalCredit { get; set; }

    /// <summary>凭证状态（0 已作废 / 1 已过账）</summary>
    public VoucherStatus Status { get; set; } = VoucherStatus.Posted;

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>更新时间</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>创建人用户 id</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>更新人用户 id</summary>
    public Guid? UpdatedBy { get; set; }
}
