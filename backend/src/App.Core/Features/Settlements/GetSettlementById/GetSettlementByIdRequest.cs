using App.Core.Abstractions;

namespace App.Core.Features.Settlements.GetSettlementById;

/// <summary>
/// 收付款单详情查询请求（只读；纯参数用例，无格式校验器）
/// </summary>
public sealed class GetSettlementByIdRequest : IRequest<SettlementDetailDto>
{
    /// <summary>收付款单 id</summary>
    public required Guid Id { get; init; }
}
