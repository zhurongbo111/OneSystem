using App.Core.Abstractions;

namespace App.Core.Features.Quotations.VoidQuotation;

/// <summary>
/// 报价单作废请求（仅改状态不删数据；仅「草稿」状态可作废，否则 40166，design.md §3.4）
/// </summary>
public sealed class VoidQuotationRequest : IRequest<QuotationDetailDto>
{
    /// <summary>报价单 id</summary>
    public required Guid Id { get; init; }
}
