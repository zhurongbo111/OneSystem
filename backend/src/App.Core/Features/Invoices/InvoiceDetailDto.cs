namespace App.Core.Features.Invoices;

/// <summary>
/// 发票详情出参模型（主表 + 关联单据明细；枚举以整型输出）
/// </summary>
public sealed class InvoiceDetailDto
{
    /// <summary>发票 id</summary>
    public required string Id { get; init; }

    /// <summary>发票号码</summary>
    public required string InvoiceNo { get; init; }

    /// <summary>发票类型（0 进项 / 1 销项）</summary>
    public required int Type { get; init; }

    /// <summary>往来单位 id</summary>
    public required string PartnerId { get; init; }

    /// <summary>往来单位名称快照</summary>
    public required string PartnerName { get; init; }

    /// <summary>开票日期</summary>
    public required DateTimeOffset InvoiceDate { get; init; }

    /// <summary>不含税金额</summary>
    public required decimal AmountExcludingTax { get; init; }

    /// <summary>税率（0–1 小数口径）</summary>
    public required decimal TaxRate { get; init; }

    /// <summary>税额（后端计算）</summary>
    public required decimal TaxAmount { get; init; }

    /// <summary>价税合计（后端计算）</summary>
    public required decimal TotalAmount { get; init; }

    /// <summary>单据状态（0 已作废 / 1 正常）</summary>
    public required int Status { get; init; }

    /// <summary>备注</summary>
    public string? Remark { get; init; }

    /// <summary>创建人用户 id</summary>
    public string? CreatedBy { get; init; }

    /// <summary>创建时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>关联单据明细（按插入顺序，快照透传）</summary>
    public required IReadOnlyList<InvoiceItemDto> Items { get; init; }
}

/// <summary>发票关联单据明细出参模型（单据号 / 日期 / 总额为开票时快照）</summary>
public sealed class InvoiceItemDto
{
    /// <summary>关联明细 id</summary>
    public required string Id { get; init; }

    /// <summary>被开票单据类型（0 采购入库 / 1 销售出库 / 2 采购退货 / 3 销售退货）</summary>
    public required int OrderType { get; init; }

    /// <summary>被开票单据 id</summary>
    public required string OrderId { get; init; }

    /// <summary>被开票单据号快照</summary>
    public required string OrderNo { get; init; }

    /// <summary>被开票单据日期快照</summary>
    public required DateTimeOffset OrderDate { get; init; }

    /// <summary>被开票单据总额快照</summary>
    public required decimal OrderTotalAmount { get; init; }

    /// <summary>本次开票金额</summary>
    public required decimal Amount { get; init; }
}