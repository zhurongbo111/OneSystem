namespace App.Core.Features.Positions;

/// <summary>
/// 岗位列表项出参模型。
/// 枚举统一以**整型**输出（PositionStatus：0 停用 / 1 启用），前端按整型渲染。
/// </summary>
public sealed class PositionListItemDto
{
    /// <summary>岗位 ID</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>岗位编码</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>岗位名称</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>岗位状态（0 停用 / 1 启用）</summary>
    public int Status { get; init; }

    /// <summary>备注</summary>
    public string? Remark { get; init; }

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>更新时间</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
