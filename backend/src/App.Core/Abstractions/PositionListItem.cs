using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 岗位列表项读模型（字段与实体 1:1，仅列表展示所需列；
/// 建立原因：仓储出参契约与 API 契约分离，specs/030-erp-org-employee/design.md §3.1）
/// </summary>
public sealed record PositionListItem
{
    /// <summary>岗位 ID</summary>
    public required Guid Id { get; init; }

    /// <summary>岗位编码</summary>
    public required string Code { get; init; }

    /// <summary>岗位名称</summary>
    public required string Name { get; init; }

    /// <summary>岗位状态</summary>
    public required PositionStatus Status { get; init; }

    /// <summary>备注，可空</summary>
    public required string? Remark { get; init; }

    /// <summary>创建时间</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>更新时间</summary>
    public required DateTimeOffset UpdatedAt { get; init; }
}
