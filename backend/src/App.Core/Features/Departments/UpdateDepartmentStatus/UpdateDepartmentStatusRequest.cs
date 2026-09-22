using App.Core.Abstractions;

namespace App.Core.Features.Departments.UpdateDepartmentStatus;

/// <summary>
/// 部门停用 / 启用请求（部门只停用不删除，保留历史与员工引用）
/// </summary>
public sealed class UpdateDepartmentStatusRequest : IRequest<DepartmentDetailDto>
{
    /// <summary>部门 id</summary>
    public Guid Id { get; init; }

    /// <summary>目标状态（0 停用 / 1 启用）</summary>
    public int Status { get; init; }
}
