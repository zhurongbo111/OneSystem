using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Departments;

/// <summary>
/// 部门出参映射（集中一处，避免各用例重复拼装）。
/// 树节点映射为递归映射（子节点随父节点一并转换）。
/// </summary>
internal static class DepartmentDtoMapper
{
    /// <summary>部门实体 → 部门详情出参</summary>
    public static DepartmentDetailDto ToDepartmentDetailDto(Department department)
        => new()
        {
            Id = department.Id.ToString(),
            Code = department.Code,
            Name = department.Name,
            ParentId = department.ParentId?.ToString(),
            SortOrder = department.SortOrder,
            Status = (int)department.Status,
            Remark = department.Remark,
            CreatedAt = department.CreatedAt,
            UpdatedAt = department.UpdatedAt,
        };

    /// <summary>部门树节点读模型 → 部门树节点出参（递归转换子节点）</summary>
    public static DepartmentTreeNodeDto ToDepartmentTreeNodeDto(DepartmentTreeNode node)
        => new()
        {
            Id = node.Id.ToString(),
            Code = node.Code,
            Name = node.Name,
            ParentId = node.ParentId?.ToString(),
            SortOrder = node.SortOrder,
            Status = (int)node.Status,
            Remark = node.Remark,
            EmployeeCount = node.EmployeeCount,
            Children = node.Children.Select(ToDepartmentTreeNodeDto).ToList(),
        };
}
