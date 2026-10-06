namespace App.Core.Features.Opportunities;

/// <summary>
/// 商机出参共享模型（与前端 DTO camelCase 一一对应）。
/// 枚举回整数值，文案与颜色映射见 specs/043-erp-crm-presale design.md §0.1（前端映射）。
/// </summary>
public sealed class OpportunityDetailDto
{
    /// <summary>商机 id</summary>
    public required string Id { get; init; }

    /// <summary>商机单号（OP + yyyyMMdd + 4 位序号）</summary>
    public required string OpportunityNo { get; init; }

    /// <summary>商机名称</summary>
    public required string Name { get; init; }

    /// <summary>来源线索 id，可空</summary>
    public string? LeadId { get; init; }

    /// <summary>关联客户 id，可空</summary>
    public string? PartnerId { get; init; }

    /// <summary>客户名称快照，可空</summary>
    public string? PartnerName { get; init; }

    /// <summary>预计金额</summary>
    public required decimal Amount { get; init; }

    /// <summary>商机阶段（0 初步接洽 / 1 需求确认 / 2 方案报价 / 3 谈判 / 4 赢单 / 5 输单）</summary>
    public required int Stage { get; init; }

    /// <summary>预计成交日期，可空</summary>
    public DateOnly? ExpectedCloseDate { get; init; }

    /// <summary>负责人（员工）id，可空</summary>
    public string? OwnerId { get; init; }

    /// <summary>负责人姓名，未指派时为空</summary>
    public string? OwnerName { get; init; }

    /// <summary>备注，可空</summary>
    public string? Remark { get; init; }

    /// <summary>创建人用户 id</summary>
    public string? CreatedBy { get; init; }

    /// <summary>创建时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>更新时间</summary>
    public required DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>商机列表行出参模型</summary>
public sealed class OpportunityListItemDto
{
    /// <summary>商机 id</summary>
    public required string Id { get; init; }

    /// <summary>商机单号</summary>
    public required string OpportunityNo { get; init; }

    /// <summary>商机名称</summary>
    public required string Name { get; init; }

    /// <summary>关联客户 id，可空</summary>
    public string? PartnerId { get; init; }

    /// <summary>客户名称快照，可空</summary>
    public string? PartnerName { get; init; }

    /// <summary>预计金额</summary>
    public required decimal Amount { get; init; }

    /// <summary>商机阶段</summary>
    public required int Stage { get; init; }

    /// <summary>预计成交日期，可空</summary>
    public DateOnly? ExpectedCloseDate { get; init; }

    /// <summary>负责人（员工）id，可空</summary>
    public string? OwnerId { get; init; }

    /// <summary>负责人姓名，未指派时为空</summary>
    public string? OwnerName { get; init; }

    /// <summary>创建时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
