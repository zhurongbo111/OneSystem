using App.Core.Abstractions;

namespace App.Core.Features.Positions.DeletePosition;

/// <summary>
/// 删除岗位请求
/// </summary>
public sealed class DeletePositionRequest : IRequest<object?>
{
    /// <summary>岗位 id</summary>
    public Guid Id { get; init; }
}
