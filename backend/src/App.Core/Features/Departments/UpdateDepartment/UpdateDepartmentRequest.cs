using App.Core.Abstractions;
using App.Core.Entities;

namespace App.Core.Features.Departments.UpdateDepartment;

/// <summary>
/// 编辑部门请求（全量覆盖语义：缺字段 / 空串一律清空，AGENTS.md §4.5；
/// 编码与名称均可改，唯一性由 Handler 校验）
/// </summary>
public sealed class UpdateDepartmentRequest : IRequest<DepartmentDetailDto>
{
    /// <summary>部门 id（取自路由，请求体缺省时由 Controller 覆盖）</summary>
    public Guid Id { get; init; }

    /// <summary>部门编码（全局唯一）</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>部门名称（同一上级下唯一）</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>上级部门 id，可空（<c>null</c> 表示顶级部门）</summary>
    public Guid? ParentId { get; init; }

    /// <summary>同级排序</summary>
    public int SortOrder { get; init; }

    /// <summary>部门状态（0 停用 / 1 启用）</summary>
    public int Status { get; init; } = (int)DepartmentStatus.Enabled;

    /// <summary>备注，可空</summary>
    public string? Remark { get; init; }
}
