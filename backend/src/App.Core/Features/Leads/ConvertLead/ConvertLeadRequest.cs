using App.Core.Abstractions;

namespace App.Core.Features.Leads.ConvertLead;

/// <summary>
/// 线索转商机请求（一次性整转；仅非终态可转，否则 40168，design.md §0.2）
/// </summary>
public sealed class ConvertLeadRequest : IRequest<ConvertLeadResultDto>
{
    /// <summary>线索 id</summary>
    public required Guid Id { get; init; }
}
