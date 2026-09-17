namespace App.Core.Entities;

/// <summary>
/// 采购订单实体（对应 PostgreSQL 表 PurchaseOrders，销售订单 <c>SalesOrder</c> 同构）。
/// 计划单据：**不直接影响库存、不追加库存流水**（specs/024-erp-order-flow design.md §1）；
/// 由采购入库单关联回写明细的累计已收数量（<c>FulfilledQuantity</c>）并推导 <see cref="OrderFlowStatus"/>。
/// 往来单位名称 / 明细商品名称与单价均为快照，后续档案修改不影响历史订单。
/// </summary>
public sealed class PurchaseOrder
{
    /// <summary>订单 ID</summary>
    public Guid Id { get; set; }

    /// <summary>订单号，唯一，后端生成（PO + yyyyMMdd + 4 位序号，如 PO202609170001）</summary>
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>供应商 ID（外键 → Partners(Id)）</summary>
    public Guid PartnerId { get; set; }

    /// <summary>供应商名称快照（列表 / 审计免 join）</summary>
    public string PartnerName { get; set; } = string.Empty;

    /// <summary>下单日期（UTC 午夜）</summary>
    public DateTimeOffset OrderDate { get; set; }

    /// <summary>预计到货日期，可空</summary>
    public DateTimeOffset? ExpectedDate { get; set; }

    /// <summary>总金额 = Σ 明细小计（后端重算，不信任前端传值）</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>订单流转状态（待收货 / 部分收货 / 已完成 / 已关闭 / 已作废，见 design.md §0）</summary>
    public OrderFlowStatus FlowStatus { get; set; } = OrderFlowStatus.Pending;

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
