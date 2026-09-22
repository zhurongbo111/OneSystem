namespace App.Core.Features.Departments;

/// <summary>
/// 部门详情出参模型（详情 / 新增 / 编辑 / 启停共用）。
/// 枚举统一以**整型**输出（DepartmentStatus：0 停用 / 1 启用），前端按整型渲染。
/// </summary>
public sealed class DepartmentDetailDto
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

    /// <summary>创建时间</summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>更新时间</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}
