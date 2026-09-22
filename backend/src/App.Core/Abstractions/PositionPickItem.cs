namespace App.Core.Abstractions;

/// <summary>
/// 岗位选择项读模型（员工表单下拉用：仅启用岗位的精简列表）
/// </summary>
public sealed record PositionPickItem
{
    /// <summary>岗位 ID</summary>
    public required Guid Id { get; init; }

    /// <summary>岗位编码</summary>
    public required string Code { get; init; }

    /// <summary>岗位名称</summary>
    public required string Name { get; init; }
}
