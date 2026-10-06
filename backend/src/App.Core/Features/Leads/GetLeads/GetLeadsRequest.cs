using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Responses;

namespace App.Core.Features.Leads.GetLeads;

/// <summary>
/// 线索分页查询请求（Query 参数绑定；只读）
/// </summary>
public sealed class GetLeadsRequest : IRequest<PagedResult<LeadListItemDto>>
{
    /// <summary>页码，从 1 起</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; init; } = 20;

    /// <summary>线索单号 / 名称 / 联系人 / 电话关键词，可空</summary>
    public string? Keyword { get; init; }

    /// <summary>线索来源，可空</summary>
    public LeadSource? Source { get; init; }

    /// <summary>线索状态，可空</summary>
    public LeadStatus? Status { get; init; }

    /// <summary>负责人（员工）id，可空</summary>
    public Guid? OwnerId { get; init; }
}
