namespace App.Core.Features.Departments;

/// <summary>
/// 部门树节点出参模型（部门树列表页数据源；子节点已按同级排序）。
/// 枚举统一以**整型**输出（DepartmentStatus：0 停用 / 1 启用），前端按整型渲染。
/// </summary>
public sealed class DepartmentTreeNodeDto
{
    /// <summary>部门 ID</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>部门编码</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>部门名称</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>上级部门 id（<c>null</c> 表示顶级部门）</summary>
    public string? ParentId { get; init; }

    /// <summary>同级排序</summary>
    public int SortOrder { get; init; }

    /// <summary>部门状态（0 停用 / 1 启用）</summary>
    public int Status { get; init; }

    /// <summary>备注</summary>
    public string? Remark { get; init; }

    /// <summary>该部门在职员工数</summary>
    public int EmployeeCount { get; init; }

    /// <summary>子部门（已按同级排序，空集合表示叶子节点）</summary>
    public IReadOnlyList<DepartmentTreeNodeDto> Children { get; init; } = [];
}
