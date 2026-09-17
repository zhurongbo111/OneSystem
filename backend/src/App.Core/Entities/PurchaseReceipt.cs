namespace App.Core.Entities;

/// <summary>
/// 采购单实体（对应 PostgreSQL 表 PurchaseReceipts，单据域共用模板，销售单 <c>SalesShipment</c> 同构）。
/// 一步式单据：保存即生效（库存立即增加）；不支持编辑，只支持作废回冲。
/// 往来单位名称 / 明细商品名称与单价均为快照，后续档案修改不影响历史单据。
/// </summary>
public sealed class PurchaseReceipt
{
    /// <summary>采购单 ID</summary>
    public Guid Id { get; set; }

    /// <summary>单号，唯一，后端生成（PO + yyyyMMdd + 4 位序号，如 PO202609110001）</summary>
    public string ReceiptNo { get; set; } = string.Empty;

    /// <summary>供应商 ID（外键 → Partners(Id)）</summary>
    public Guid PartnerId { get; set; }

    /// <summary>供应商名称快照（列表 / 审计免 join）</summary>
    public string PartnerName { get; set; } = string.Empty;

    /// <summary>业务日期（UTC 午夜）</summary>
    public DateTimeOffset OrderDate { get; set; }

    /// <summary>总金额 = Σ 明细小计（后端重算，不信任前端传值）</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>已结算金额（由收付款单核销累加 / 作废回退，不允许手工直接改；specs/023-erp-settlement/design.md §2.3）</summary>
    public decimal SettledAmount { get; set; }

    /// <summary>单据状态（1=正常 0=已作废；作废后禁止再操作）</summary>
    public OrderStatus Status { get; set; } = OrderStatus.Normal;

    /// <summary>关联采购订单 id（可空：不关联订单时为空，沿用「货到即入账」直通用法，specs/024-erp-order-flow design.md §2.3）</summary>
    public Guid? OrderId { get; set; }

    /// <summary>关联采购订单号快照（入库单列表 / 详情免 join 订单表；不关联时为空）</summary>
    public string? OrderNo { get; set; }

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
