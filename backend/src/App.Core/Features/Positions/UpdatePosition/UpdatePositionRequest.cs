using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Positions.UpdatePosition;

/// <summary>
/// 编辑岗位请求（全量覆盖语义：空串一律清空，AGENTS.md §4.5；编码与名称均可改）
/// </summary>
public sealed class UpdatePositionRequest : IRequest<PositionDetailDto>
{
    /// <summary>岗位 id（取自路由，请求体缺省时由 Controller 覆盖）</summary>
    public Guid Id { get; init; }

    /// <summary>岗位编码（全局唯一）</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>岗位名称（全局唯一）</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>岗位状态（0 停用 / 1 启用）</summary>
    public int Status { get; init; } = (int)PositionStatus.Enabled;

    /// <summary>备注，可空</summary>
    public string? Remark { get; init; }
}
