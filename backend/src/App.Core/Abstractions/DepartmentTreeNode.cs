using App.Core.Entities;

namespace App.Core.Abstractions;

/// <summary>
/// 部门树节点读模型（仓储出参契约，不暴露到 API）。
/// 仓储一次取全量部门并组装为树（<see cref="Children"/> 已按同级排序填充），
/// 在职员工数（<see cref="EmployeeCount"/>）为联查汇总字段
/// （specs/030-erp-org-employee/design.md §3.1）。
/// </summary>
public sealed record DepartmentTreeNode
{
    /// <summary>部门 ID</summary>
    public required Guid Id { get; init; }

    /// <summary>部门编码</summary>
    public required string Code { get; init; }

    /// <summary>部门名称</summary>
    public required string Name { get; init; }

    /// <summary>上级部门 id（<c>null</c> 表示顶级）</summary>
    public required Guid? ParentId { get; init; }

    /// <summary>同级排序</summary>
    public required int SortOrder { get; init; }

    /// <summary>部门状态</summary>
    public required DepartmentStatus Status { get; init; }

    /// <summary>备注，可空</summary>
    public required string? Remark { get; init; }

    /// <summary>该部门在职员工数（联查汇总带出）</summary>
    public required int EmployeeCount { get; init; }

    /// <summary>子部门（已按同级排序，空集合表示叶子节点）</summary>
    public required IReadOnlyList<DepartmentTreeNode> Children { get; init; }
}
