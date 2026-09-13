using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Responses;

namespace App.Core.Features.Partners.GetPartners;

/// <summary>
/// 往来单位分页查询请求
/// </summary>
public sealed class GetPartnersRequest : IRequest<PagedResult<PartnerDto>>
{
    /// <summary>页码，从 1 起</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; init; } = 20;

    /// <summary>关键词（名称 / 联系人模糊匹配），可空</summary>
    public string? Keyword { get; init; }

    /// <summary>单位类型（1 供应商 / 2 客户 / 3 两者），可空</summary>
    public PartnerType? Type { get; init; }

    /// <summary>单位状态（0 停用 / 1 启用），可空</summary>
    public PartnerStatus? Status { get; init; }
}
