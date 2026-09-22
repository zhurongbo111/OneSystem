using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Features.GeneralLedger;
using App.Core.Responses;

namespace App.Core.Features.Vouchers.GetVouchers;

/// <summary>
/// 凭证分页查询请求（Query 参数绑定；只读）
/// </summary>
public sealed class GetVouchersRequest : IRequest<PagedResult<VoucherListItemDto>>
{
    /// <summary>页码，从 1 起</summary>
    public int Page { get; init; } = 1;

    /// <summary>每页条数</summary>
    public int PageSize { get; init; } = 20;

    /// <summary>归属期间年，可空（与 <see cref="Month"/> 同时传才生效）</summary>
    public int? Year { get; init; }

    /// <summary>归属期间月，可空（与 <see cref="Year"/> 同时传才生效）</summary>
    public int? Month { get; init; }

    /// <summary>来源类型，可空</summary>
    public VoucherSourceType? SourceType { get; init; }

    /// <summary>凭证号 / 摘要关键词，可空</summary>
    public string? Keyword { get; init; }
}
