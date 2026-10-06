using App.Core.Abstractions;

namespace App.Core.Features.Leads.GetLeadById;

/// <summary>
/// 线索详情查询请求（只读）
/// </summary>
public sealed class GetLeadByIdRequest : IRequest<LeadDetailDto>
{
    /// <summary>线索 id</summary>
    public required Guid Id { get; init; }
}
