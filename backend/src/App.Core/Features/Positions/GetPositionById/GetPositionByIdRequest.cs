using App.Core.Abstractions;

namespace App.Core.Features.Positions.GetPositionById;

/// <summary>
/// 岗位详情请求
/// </summary>
public sealed class GetPositionByIdRequest : IRequest<PositionDetailDto>
{
    /// <summary>岗位 id</summary>
    public Guid Id { get; init; }
}
