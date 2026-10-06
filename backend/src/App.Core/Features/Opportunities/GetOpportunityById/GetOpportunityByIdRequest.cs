using App.Core.Abstractions;

namespace App.Core.Features.Opportunities.GetOpportunityById;

/// <summary>
/// 商机详情查询请求（只读）
/// </summary>
public sealed class GetOpportunityByIdRequest : IRequest<OpportunityDetailDto>
{
    /// <summary>商机 id</summary>
    public required Guid Id { get; init; }
}
