using App.Core.Abstractions;

namespace App.Core.Features.Quotations.ConvertToOrder;

/// <summary>
/// 报价单转销售订单请求（一次性整单转；仅「草稿」状态可转，否则 40167，design.md §0.3）
/// </summary>
public sealed class ConvertQuotationRequest : IRequest<ConvertQuotationResultDto>
{
    /// <summary>报价单 id</summary>
    public required Guid Id { get; init; }
}
