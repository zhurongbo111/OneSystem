using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 发票列表行读模型（仓储出参契约，不暴露到 API）。
/// <see cref="OrderNoSummary"/> 为关联单据号拼接（跨表聚合字段，实体上没有），故本域建读模型；
/// 其余字段与 <see cref="Invoice"/> 1:1。
/// </summary>
public sealed record InvoiceListItem
{
    /// <summary>发票 id</summary>
    public required Guid Id { get; init; }

    /// <summary>发票号码</summary>
    public required string InvoiceNo { get; init; }

    /// <summary>发票类型（0 进项 / 1 销项）</summary>
    public required InvoiceType Type { get; init; }

    /// <summary>往来单位名称快照</summary>
    public required string PartnerName { get; init; }

    /// <summary>开票日期</summary>
    public required DateTimeOffset InvoiceDate { get; init; }

    /// <summary>不含税金额</summary>
    public required decimal AmountExcludingTax { get; init; }

    /// <summary>税率（0–1 小数口径）</summary>
    public required decimal TaxRate { get; init; }

    /// <summary>税额</summary>
    public required decimal TaxAmount { get; init; }

    /// <summary>价税合计</summary>
    public required decimal TotalAmount { get; init; }

    /// <summary>单据状态（0 已作废 / 1 正常）</summary>
    public required OrderStatus Status { get; init; }

    /// <summary>创建时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>关联单据号拼接（无明细时为空串，列表展示用）</summary>
    public required string OrderNoSummary { get; init; }
}