namespace App.Core.Entities;

/// <summary>
/// 会计期间实体（对应 PostgreSQL 表 AccountingPeriods，specs/033-erp-general-ledger/design.md §2.1）。
/// 按「年-月」唯一；凭证归属由 <see cref="Voucher.VoucherDate"/> 的年月决定；
/// 已结账期间禁止新增 / 作废凭证（须先反结账）。
/// </summary>
public sealed class AccountingPeriod
{
    /// <summary>期间 ID</summary>
    public Guid Id { get; set; }

    /// <summary>年</summary>
    public int Year { get; set; }

    /// <summary>月（1–12）</summary>
    public int Month { get; set; }

    /// <summary>期间状态（0 未结账 / 1 已结账）</summary>
    public PeriodStatus Status { get; set; } = PeriodStatus.Open;

    /// <summary>结账时间，未结账为空</summary>
    public DateTimeOffset? ClosedAt { get; set; }

    /// <summary>结账人用户 id，未结账为空</summary>
    public Guid? ClosedBy { get; set; }
}
