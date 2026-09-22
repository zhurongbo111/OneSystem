using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Invoices.CreateInvoice;

/// <summary>
/// 登记发票请求（发票号由用户录入且全局唯一；税额与价税合计由后端按税率重算，不信任前端传值）
/// </summary>
public sealed class CreateInvoiceRequest : IRequest<InvoiceDetailDto>
{
    /// <summary>发票号码（全局唯一）</summary>
    public string InvoiceNo { get; init; } = string.Empty;

    /// <summary>发票类型（0 进项 / 1 销项）</summary>
    public required InvoiceType Type { get; init; }

    /// <summary>往来单位 id（进项为供应商、销项为客户）</summary>
    public required Guid PartnerId { get; init; }

    /// <summary>开票日期（UTC 午夜，前端所选日期的本地 0 点转 UTC ISO 串）</summary>
    public required DateTimeOffset InvoiceDate { get; init; }

    /// <summary>不含税金额（&gt; 0）</summary>
    public required decimal AmountExcludingTax { get; init; }

    /// <summary>税率（0–1 小数口径，如 0.13；可为 0）</summary>
    public required decimal TaxRate { get; init; }

    /// <summary>关联单据明细行（1–100 行；orderType / orderId / amount）</summary>
    public required IReadOnlyList<CreateInvoiceItem> Items { get; init; }

    /// <summary>备注，可空</summary>
    public string? Remark { get; init; }
}

/// <summary>关联单据明细行入参（单据号 / 日期 / 总额快照由后端从被关联单据带出）</summary>
public sealed class CreateInvoiceItem
{
    /// <summary>被开票单据类型（0 采购入库 / 1 销售出库 / 2 采购退货 / 3 销售退货）</summary>
    public required SettlementOrderType OrderType { get; init; }

    /// <summary>被开票单据 id</summary>
    public required Guid OrderId { get; init; }

    /// <summary>本次开票金额（&gt; 0，且不超过该单据未开票金额）</summary>
    public required decimal Amount { get; init; }
}