using App.Core.Abstractions;

namespace App.Core.Features.Positions.UpdatePositionStatus;

/// <summary>
/// 岗位停用 / 启用请求（岗位只停用不删除，保留历史与员工引用）
/// </summary>
public sealed class UpdatePositionStatusRequest : IRequest<PositionDetailDto>
{
    /// <summary>岗位 id</summary>
    public Guid Id { get; init; }

    /// <summary>目标状态（0 停用 / 1 启用）</summary>
    public int Status { get; init; }
}
