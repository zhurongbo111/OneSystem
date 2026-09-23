namespace App.Core.Entities;

/// <summary>
/// 报价单实体（对应 PostgreSQL 表 Quotations）。
/// **纯意向单**：不锁库存、不写库存流水、不产生应收（specs/037-erp-quotation design.md §1）；
/// 转销售订单后置 <see cref="QuotationStatus.Converted"/> 并回写 <see cref="ConvertedOrderId"/> / <see cref="ConvertedOrderNo"/>。
/// 客户名称与明细商品名称 / 单价均为快照，后续档案修改不影响历史报价单。
/// </summary>
public sealed class Quotation
{
    /// <summary>报价单 ID</summary>
    public Guid Id { get; set; }

    /// <summary>报价单号，唯一，后端生成（QT + yyyyMMdd + 4 位序号，如 QT202609230001）</summary>
    public string QuotationNo { get; set; } = string.Empty;

    /// <summary>客户 ID（外键 → Partners(Id)）</summary>
    public Guid PartnerId { get; set; }

    /// <summary>客户名称快照（列表 / 审计免 join）</summary>
    public string PartnerName { get; set; } = string.Empty;

    /// <summary>报价日期（UTC 午夜）</summary>
    public DateTimeOffset QuotationDate { get; set; }

    /// <summary>报价有效期至（纯日期，可空；过期仅展示「已过期」，不自动改状态）</summary>
    public DateOnly? ValidUntil { get; set; }

    /// <summary>总金额 = Σ 明细小计（后端重算，不信任前端传值）</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>报价单状态（草稿 / 已转订单 / 已作废，见 design.md §0.1）</summary>
    public QuotationStatus Status { get; set; } = QuotationStatus.Draft;

    /// <summary>转出的销售订单 id，可空（同一报价单只能转一次）</summary>
    public Guid? ConvertedOrderId { get; set; }

    /// <summary>转出的销售订单号快照，可空</summary>
    public string? ConvertedOrderNo { get; set; }

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
