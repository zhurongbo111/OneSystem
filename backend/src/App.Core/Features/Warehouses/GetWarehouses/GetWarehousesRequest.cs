using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Responses;

namespace App.Core.Features.Warehouses.GetWarehouses;

/// <summary>
/// 仓库分页查询请求
/// </summary>
public sealed class GetWarehousesRequest : IRequest<PagedResult<WarehouseDto>>
{
    /// <summary>页码，从 1 起</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; init; } = 20;

    /// <summary>关键词（编码 / 名称模糊匹配），可空</summary>
    public string? Keyword { get; init; }

    /// <summary>仓库状态（0 停用 / 1 启用），可空</summary>
    public PartnerStatus? Status { get; init; }
}
