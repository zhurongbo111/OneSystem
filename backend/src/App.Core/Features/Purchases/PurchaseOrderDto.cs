namespace App.Core.Features.Purchases;

/// <summary>
/// 采购单出参共享模型（主表 + 明细，与后端 DTO camelCase 一一对应；列表行用 PurchaseOrderListItemDto）
/// </summary>
public sealed class PurchaseOrderDetailDto
{
    /// <summary>采购单 id</summary>
    public required string Id { get; init; }

    /// <summary>单号</summary>
    public required string OrderNo { get; init; }

    /// <summary>供应商 id</summary>
    public required string PartnerId { get; init; }

    /// <summary>供应商名称快照</summary>
    public required string PartnerName { get; init; }

    /// <summary>业务日期</summary>
    public required DateTimeOffset OrderDate { get; init; }

    /// <summary>总金额（后端重算值）</summary>
    public required decimal TotalAmount { get; init; }

    /// <summary>已结算金额（由收付款单核销累加 / 作废回退）</summary>
    public required decimal SettledAmount { get; init; }

    /// <summary>未结金额（= 总额 − 已结算金额，推导值）</summary>
    public required decimal UnsettledAmount { get; init; }

    /// <summary>结算状态（0 未结算 / 1 部分结算 / 2 已结算，推导值）</summary>
    public required int SettlementState { get; init; }

    /// <summary>单据状态（0 已作废 / 1 正常）</summary>
    public required int Status { get; init; }

    /// <summary>备注</summary>
    public string? Remark { get; init; }

    /// <summary>创建人用户 id</summary>
    public string? CreatedBy { get; init; }

    /// <summary>创建时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>明细行（按插入顺序）</summary>
    public required IReadOnlyList<PurchaseOrderItemDto> Items { get; init; }
}

/// <summary>采购单明细出参模型（快照字段原样返回）</summary>
public sealed class PurchaseOrderItemDto
{
    /// <summary>明细行 id</summary>
    public required string Id { get; init; }

    /// <summary>商品 id</summary>
    public required string ProductId { get; init; }

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

/// <summary>采购单列表行出参模型（含 Status 供前端作废行置灰）</summary>
public sealed class PurchaseOrderListItemDto
{
    /// <summary>采购单 id</summary>
    public required string Id { get; init; }

    /// <summary>单号</summary>
    public required string OrderNo { get; init; }

    /// <summary>供应商 id</summary>
    public required string PartnerId { get; init; }

    /// <summary>供应商名称快照</summary>
    public required string PartnerName { get; init; }

    /// <summary>业务日期</summary>
    public required DateTimeOffset OrderDate { get; init; }

    /// <summary>总金额</summary>
    public required decimal TotalAmount { get; init; }

    /// <summary>已结算金额（由收付款单核销累加 / 作废回退）</summary>
    public required decimal SettledAmount { get; init; }

    /// <summary>未结金额（= 总额 − 已结算金额，推导值）</summary>
    public required decimal UnsettledAmount { get; init; }

    /// <summary>结算状态（0 未结算 / 1 部分结算 / 2 已结算，推导值）</summary>
    public required int SettlementState { get; init; }

    /// <summary>单据状态（0 已作废 / 1 正常）</summary>
    public required int Status { get; init; }

    /// <summary>创建时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
