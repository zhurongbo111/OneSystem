namespace App.Core.Entities;

/// <summary>
/// 线索实体（对应 PostgreSQL 表 Leads）。
/// **售前意向数据**：不锁库存、不写库存流水、不产生应收（specs/043-erp-crm-presale design.md §1）；
/// 转商机后置 <see cref="LeadStatus.Converted"/> 并回写 <see cref="OpportunityId"/> / <see cref="OpportunityNo"/>（一条线索只能转一次）。
/// 负责人关联 <see cref="Employee"/>（`030`），不感知系统账号。
/// </summary>
public sealed class Lead
{
    /// <summary>线索 ID</summary>
    public Guid Id { get; set; }

    /// <summary>线索单号，唯一，后端生成（LD + yyyyMMdd + 4 位序号，如 LD202610060001）</summary>
    public string LeadNo { get; set; } = string.Empty;

    /// <summary>线索名称 / 公司</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>联系人，可空</summary>
    public string? Contact { get; set; }

    /// <summary>联系电话，可空</summary>
    public string? Phone { get; set; }

    /// <summary>线索来源（默认其他）</summary>
    public LeadSource Source { get; set; } = LeadSource.Other;

    /// <summary>线索状态（默认新线索；已转化 / 已废弃为终态）</summary>
    public LeadStatus Status { get; set; } = LeadStatus.New;

    /// <summary>负责人（员工）id，可空（外键 → Employees(Id)）</summary>
    public Guid? OwnerId { get; set; }

    /// <summary>转出的商机 id，可空（转商机时回填）</summary>
    public Guid? OpportunityId { get; set; }

    /// <summary>转出的商机号快照，可空</summary>
    public string? OpportunityNo { get; set; }

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
