namespace App.Core.Entities;

/// <summary>
/// 发票实体（对应 PostgreSQL 表 Invoices，specs/032-erp-invoice/design.md §2.1）。
/// 一张发票可关联多张单据（明细见 <see cref="InvoiceItem"/>），支持部分开票；可作废（占用金额按聚合自动释放）。
/// 往来名称为快照；税额与价税合计由后端按税率重算（不信任前端传值）。
/// </summary>
public sealed class Invoice
{
    /// <summary>发票 ID</summary>
    public Guid Id { get; set; }

    /// <summary>发票号码（全局唯一，手工录入）</summary>
    public string InvoiceNo { get; set; } = string.Empty;

    /// <summary>发票类型（0=进项 1=销项）</summary>
    public InvoiceType Type { get; set; }

    /// <summary>往来单位 ID（外键 → Partners(Id)；进项为供应商、销项为客户）</summary>
    public Guid PartnerId { get; set; }

    /// <summary>往来单位名称快照（列表 / 详情免 join）</summary>
    public string PartnerName { get; set; } = string.Empty;

    /// <summary>开票日期（UTC 午夜）</summary>
    public DateTimeOffset InvoiceDate { get; set; }

    /// <summary>不含税金额（&gt; 0，用户录入）</summary>
    public decimal AmountExcludingTax { get; set; }

    /// <summary>税率（0–1 小数口径，如 0.13；可为 0）</summary>
    public decimal TaxRate { get; set; }

    /// <summary>税额 = Round(不含税金额 × 税率, 2)（后端计算）</summary>
    public decimal TaxAmount { get; set; }

    /// <summary>价税合计 = 不含税金额 + 税额（后端计算）</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>单据状态（1=正常 0=已作废；作废为终态，作废后占用金额自动释放）</summary>
    public OrderStatus Status { get; set; } = OrderStatus.Normal;

    /// <summary>备注</summary>
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