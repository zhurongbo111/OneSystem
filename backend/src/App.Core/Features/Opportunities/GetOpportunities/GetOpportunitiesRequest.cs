using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Responses;

namespace App.Core.Features.Opportunities.GetOpportunities;

/// <summary>
/// 商机分页查询请求（Query 参数绑定；只读）
/// </summary>
public sealed class GetOpportunitiesRequest : IRequest<PagedResult<OpportunityListItemDto>>
{
    /// <summary>页码，从 1 起</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; init; } = 20;

    /// <summary>商机单号 / 名称关键词，可空</summary>
    public string? Keyword { get; init; }

    /// <summary>商机阶段，可空</summary>
    public OpportunityStage? Stage { get; init; }

    /// <summary>关联客户 id，可空</summary>
    public Guid? PartnerId { get; init; }

    /// <summary>负责人（员工）id，可空</summary>
    public Guid? OwnerId { get; init; }
}
