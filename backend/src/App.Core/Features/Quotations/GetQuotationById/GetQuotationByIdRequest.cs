using App.Core.Abstractions;

namespace App.Core.Features.Quotations.GetQuotationById;

/// <summary>
/// 报价单详情查询请求（只读；纯参数用例，无格式校验器）
/// </summary>
public sealed class GetQuotationByIdRequest : IRequest<QuotationDetailDto>
{
    /// <summary>报价单 id</summary>
    public required Guid Id { get; init; }
}
