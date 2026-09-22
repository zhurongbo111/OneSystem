using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Positions.CreatePosition;

/// <summary>
/// 新增岗位请求
/// </summary>
public sealed class CreatePositionRequest : IRequest<PositionDetailDto>
{
    /// <summary>岗位编码（全局唯一）</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>岗位名称（全局唯一）</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>岗位状态（0 停用 / 1 启用，默认启用）</summary>
    public int Status { get; init; } = (int)PositionStatus.Enabled;

    /// <summary>备注，可空</summary>
    public string? Remark { get; init; }
}
