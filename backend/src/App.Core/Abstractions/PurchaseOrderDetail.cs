using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 采购单详情读模型（主表 + 明细行，按明细插入顺序）
/// </summary>
public sealed record PurchaseOrderDetail
{
    /// <summary>采购单 ID</summary>
    public required Guid Id { get; init; }

    /// <summary>单号</summary>
    public required string OrderNo { get; init; }

    /// <summary>供应商 ID</summary>
    public required Guid PartnerId { get; init; }

    /// <summary>供应商名称快照</summary>
    public required string PartnerName { get; init; }

    /// <summary>业务日期</summary>
    public required DateTimeOffset OrderDate { get; init; }

    /// <summary>总金额（后端重算值）</summary>
    public required decimal TotalAmount { get; init; }

    /// <summary>结算状态</summary>
    public required OrderSettlementStatus SettlementStatus { get; init; }

    /// <summary>单据状态</summary>
    public required OrderStatus Status { get; init; }

    /// <summary>备注</summary>
    public string? Remark { get; init; }

    /// <summary>创建人用户 id</summary>
    public Guid? CreatedBy { get; init; }

    /// <summary>创建时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>明细行（按插入顺序）</summary>
    public required IReadOnlyList<PurchaseOrderDetailItem> Items { get; init; }
}

/// <summary>采购单明细读模型（快照字段原样返回）</summary>
public sealed record PurchaseOrderDetailItem
{
    /// <summary>明细行 ID</summary>
    public required Guid Id { get; init; }

    /// <summary>商品 ID</summary>
    public required Guid ProductId { get; init; }

    /// <summary>商品名称快照</summary>
    public required string ProductName { get; init; }

    /// <summary>计量单位快照</summary>
    public required string Unit { get; init; }

    /// <summary>数量</summary>
    public required int Quantity { get; init; }

    /// <summary>单价快照</summary>
    public required decimal UnitPrice { get; init; }

    /// <summary>小计（后端重算值）</summary>
    public required decimal Subtotal { get; init; }
}
