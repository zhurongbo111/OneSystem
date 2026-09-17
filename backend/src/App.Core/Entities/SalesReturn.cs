namespace App.Core.Entities;

/// <summary>
/// 销售退货单实体（对应 PostgreSQL 表 SalesReturns，采购退货单 <c>PurchaseReturn</c> 同构，见 specs/021-erp-purchase-return/design.md §0）。
/// 一步式单据：保存即生效（库存立即回增、应收口径冲减）；不支持编辑，只支持作废回冲。
/// 客户名称 / 明细商品名称与单价均为快照，后续档案修改不影响历史单据。
/// 不关联原销售单（见 specs/021-erp-purchase-return/design.md §5）：备注可写原销售单号。
/// </summary>
public sealed class SalesReturn
{
    /// <summary>销售退货单 ID</summary>
    public Guid Id { get; set; }

    /// <summary>单号，唯一，后端生成（SR + yyyyMMdd + 4 位序号，如 SR202609160001）</summary>
    public string ReturnNo { get; set; } = string.Empty;

    /// <summary>客户 ID（外键 → Partners(Id)）</summary>
    public Guid PartnerId { get; set; }

    /// <summary>客户名称快照（列表 / 审计免 join）</summary>
    public string PartnerName { get; set; } = string.Empty;

    /// <summary>业务日期（UTC 午夜）</summary>
    public DateTimeOffset ReturnDate { get; set; }

    /// <summary>总金额 = Σ 明细小计（后端重算，不信任前端传值）</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>结算状态（0=未结算 1=已结算；复用单据域枚举，升级路径见 specs/ROADMAP.md §4.3）</summary>
    public OrderSettlementStatus SettlementStatus { get; set; }

    /// <summary>单据状态（1=正常 0=已作废；作废后禁止再操作）</summary>
    public OrderStatus Status { get; set; } = OrderStatus.Normal;

    /// <summary>备注（可写原销售单号 / 退货原因）</summary>
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
