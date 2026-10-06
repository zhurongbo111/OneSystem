namespace App.Core.Features.Leads;

/// <summary>
/// 线索出参共享模型（与前端 DTO camelCase 一一对应）。
/// 枚举回整数值，文案与颜色映射见 specs/043-erp-crm-presale design.md §0.1（前端映射）。
/// </summary>
public sealed class LeadDetailDto
{
    /// <summary>线索 id</summary>
    public required string Id { get; init; }

    /// <summary>线索单号（LD + yyyyMMdd + 4 位序号）</summary>
    public required string LeadNo { get; init; }

    /// <summary>线索名称 / 公司</summary>
    public required string Name { get; init; }

    /// <summary>联系人，可空</summary>
    public string? Contact { get; init; }

    /// <summary>联系电话，可空</summary>
    public string? Phone { get; init; }

    /// <summary>线索来源（0 网站 / 1 电话 / 2 推荐 / 3 展会 / 4 其他）</summary>
    public required int Source { get; init; }

    /// <summary>线索状态（0 新线索 / 1 跟进中 / 2 已转化 / 3 已废弃）</summary>
    public required int Status { get; init; }

    /// <summary>负责人（员工）id，可空</summary>
    public string? OwnerId { get; init; }

    /// <summary>负责人姓名，未指派时为空</summary>
    public string? OwnerName { get; init; }

    /// <summary>转出的商机 id，可空</summary>
    public string? OpportunityId { get; init; }

    /// <summary>转出的商机号快照，可空</summary>
    public string? OpportunityNo { get; init; }

    /// <summary>备注，可空</summary>
    public string? Remark { get; init; }

    /// <summary>创建人用户 id</summary>
    public string? CreatedBy { get; init; }

    /// <summary>创建时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>更新时间</summary>
    public required DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>线索列表行出参模型</summary>
public sealed class LeadListItemDto
{
    /// <summary>线索 id</summary>
    public required string Id { get; init; }

    /// <summary>线索单号</summary>
    public required string LeadNo { get; init; }

    /// <summary>线索名称 / 公司</summary>
    public required string Name { get; init; }

    /// <summary>联系人，可空</summary>
    public string? Contact { get; init; }

    /// <summary>联系电话，可空</summary>
    public string? Phone { get; init; }

    /// <summary>线索来源</summary>
    public required int Source { get; init; }

    /// <summary>线索状态</summary>
    public required int Status { get; init; }

    /// <summary>负责人（员工）id，可空</summary>
    public string? OwnerId { get; init; }

    /// <summary>负责人姓名，未指派时为空</summary>
    public string? OwnerName { get; init; }

    /// <summary>转出的商机号快照，可空</summary>
    public string? OpportunityNo { get; init; }

    /// <summary>创建时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// 线索转商机结果出参（design.md §3.3：`{ opportunityId, opportunityNo }`）：
/// 前端据此提示线索已转化并可跳转商机详情。
/// </summary>
public sealed class ConvertLeadResultDto
{
    /// <summary>生成的商机 id</summary>
    public required string OpportunityId { get; init; }

    /// <summary>生成的商机号</summary>
    public required string OpportunityNo { get; init; }
}
