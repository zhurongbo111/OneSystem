namespace App.Core.Entities;

/// <summary>
/// 销售单实体（对应 PostgreSQL 表 SalesOrders，与 <c>PurchaseOrder</c> 同构）。
/// 一步式单据：保存即生效（库存立即减少）；不支持编辑，只支持作废回冲。
/// 客户名称 / 明细商品名称与单价均为快照，后续档案修改不影响历史单据。
/// </summary>
public sealed class SalesOrder
{
    /// <summary>销售单 ID</summary>
    public Guid Id { get; set; }

    /// <summary>单号，唯一，后端生成（SO + yyyyMMdd + 4 位序号，如 SO202609110001）</summary>
    public string OrderNo { get; set; } = string.Empty;

    /// <summary>客户 ID（外键 → Partners(Id)）</summary>
    public Guid PartnerId { get; set; }

    /// <summary>客户名称快照（列表 / 审计免 join）</summary>
    public string PartnerName { get; set; } = string.Empty;

    /// <summary>业务日期（UTC 午夜）</summary>
    public DateTimeOffset OrderDate { get; set; }

    /// <summary>总金额 = Σ 明细小计（后端重算，不信任前端传值）</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>结算状态（0=未收 1=已收）</summary>
    public OrderSettlementStatus SettlementStatus { get; set; }

    /// <summary>单据状态（1=正常 0=已作废；作废后禁止再操作）</summary>
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
