namespace App.Core.Features.Positions;

/// <summary>
/// 岗位选择项出参模型（员工表单下拉用：仅启用岗位的精简列表）
/// </summary>
public sealed class PositionPickDto
{
    /// <summary>岗位 ID</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>岗位编码</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>岗位名称</summary>
    public string Name { get; init; } = string.Empty;
}
