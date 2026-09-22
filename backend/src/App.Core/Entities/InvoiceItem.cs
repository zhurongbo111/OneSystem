namespace App.Core.Entities;

/// <summary>
/// 发票关联单据明细实体（对应 PostgreSQL 表 InvoiceItems，specs/032-erp-invoice/design.md §2.2）。
/// 明细不软删除、不可改；单据号 / 单据日期 / 单据总额均为开票时快照，详情页免跨四表联查。
/// </summary>
public sealed class InvoiceItem
{
    /// <summary>明细 ID</summary>
    public Guid Id { get; set; }

    /// <summary>所属发票 ID（外键 → Invoices(Id)）</summary>
    public Guid InvoiceId { get; set; }

    /// <summary>被开票单据类型（复用 <see cref="SettlementOrderType"/>，语义为「可关联单据类型」）</summary>
    public SettlementOrderType OrderType { get; set; }

    /// <summary>被开票单据 id</summary>
    public Guid OrderId { get; set; }

    /// <summary>被开票单据号快照</summary>
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>被开票单据日期快照</summary>
    public DateTimeOffset OrderDate { get; set; }

    /// <summary>被开票单据总额快照</summary>
    public decimal OrderTotalAmount { get; set; }

    /// <summary>本次开票金额（&gt; 0）</summary>
    public decimal Amount { get; set; }
}