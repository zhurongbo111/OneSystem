namespace App.Core.Features.Invoices;

/// <summary>
/// 可开票单据候选出参模型（新建发票页选择关联单据用；未开票金额由后端推导）
/// </summary>
public sealed class InvoicableOrderDto
{
    /// <summary>被开票单据类型（0 采购入库 / 1 销售出库 / 2 采购退货 / 3 销售退货）</summary>
    public required int OrderType { get; init; }

    /// <summary>被开票单据 id</summary>
    public required string OrderId { get; init; }

    /// <summary>被开票单据号</summary>
    public required string OrderNo { get; init; }

    /// <summary>被开票单据业务日期</summary>
    public required DateTimeOffset OrderDate { get; init; }

    /// <summary>单据总额</summary>
    public required decimal TotalAmount { get; init; }

    /// <summary>已开票金额</summary>
    public required decimal InvoicedAmount { get; init; }

    /// <summary>未开票金额（= 总额 − 已开票金额，推导值）</summary>
    public required decimal UninvoicedAmount { get; init; }
}