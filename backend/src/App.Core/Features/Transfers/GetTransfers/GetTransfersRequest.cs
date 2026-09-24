using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.Transfers.GetTransfers;

/// <summary>
/// 调拨单分页查询请求（Query 参数绑定；只读）。筛选维度：单号关键词 + 转出 / 转入仓 + 日期闭区间。
/// </summary>
public sealed class GetTransfersRequest : IRequest<PagedResult<TransferListItemDto>>
{
    /// <summary>页码，从 1 起</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; init; } = 20;

    /// <summary>单号关键词，可空（匹配 TransferNo，忽略大小写）</summary>
    public string? Keyword { get; init; }

    /// <summary>转出仓 id，可空（不传 = 全部仓）</summary>
    public Guid? FromWarehouseId { get; init; }

    /// <summary>转入仓 id，可空（不传 = 全部仓）</summary>
    public Guid? ToWarehouseId { get; init; }

    /// <summary>起始业务日期（含），可空</summary>
    public DateTimeOffset? Start { get; init; }

    /// <summary>结束业务日期（含），可空</summary>
    public DateTimeOffset? End { get; init; }
}
