using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Departments.CreateDepartment;

/// <summary>
/// 新增部门请求
/// </summary>
public sealed class CreateDepartmentRequest : IRequest<DepartmentDetailDto>
{
    /// <summary>部门编码（全局唯一）</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>部门名称（同一上级下唯一）</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>上级部门 id，可空（<c>null</c> 表示顶级部门）</summary>
    public Guid? ParentId { get; init; }

    /// <summary>同级排序（默认 0）</summary>
    public int SortOrder { get; init; }

    /// <summary>部门状态（0 停用 / 1 启用，默认启用）</summary>
    public int Status { get; init; } = (int)DepartmentStatus.Enabled;

    /// <summary>备注，可空</summary>
    public string? Remark { get; init; }
}
