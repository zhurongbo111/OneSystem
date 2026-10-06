namespace App.Core.Entities;

/// <summary>
/// 商机实体（对应 PostgreSQL 表 Opportunities）。
/// **售前意向数据**：不锁库存、不写库存流水、不产生应收（specs/043-erp-crm-presale design.md §1）；
/// 客户可空（线索阶段可能尚无正式客户档案，赢单前可补），客户名称落快照列、后续档案修改不影响历史。
/// 来源线索 <see cref="LeadId"/> 可空（手工新建商机无来源线索）。
/// </summary>
public sealed class Opportunity
{
    /// <summary>商机 ID</summary>
    public Guid Id { get; set; }

    /// <summary>商机单号，唯一，后端生成（OP + yyyyMMdd + 4 位序号，如 OP202610060001）</summary>
    public string OpportunityNo { get; set; } = string.Empty;

    /// <summary>商机名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>来源线索 id，可空（外键 → Leads(Id)）</summary>
    public Guid? LeadId { get; set; }

    /// <summary>关联客户 id，可空（外键 → Partners(Id)）</summary>
    public Guid? PartnerId { get; set; }

    /// <summary>客户名称快照，可空</summary>
    public string? PartnerName { get; set; }

    /// <summary>预计金额（≥ 0，numeric(18,2)）</summary>
    public decimal Amount { get; set; }

    /// <summary>商机阶段（默认初步接洽；赢单 / 输单为终态）</summary>
    public OpportunityStage Stage { get; set; } = OpportunityStage.Initial;

    /// <summary>预计成交日期（纯日期，可空）</summary>
    public DateOnly? ExpectedCloseDate { get; set; }

    /// <summary>负责人（员工）id，可空（外键 → Employees(Id)）</summary>
    public Guid? OwnerId { get; set; }

    /// <summary>备注，可空</summary>
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
