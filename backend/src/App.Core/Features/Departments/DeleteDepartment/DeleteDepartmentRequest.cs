using App.Core.Abstractions;

namespace App.Core.Features.Departments.DeleteDepartment;

/// <summary>
/// 删除部门请求
/// </summary>
public sealed class DeleteDepartmentRequest : IRequest<object?>
{
    /// <summary>部门 id</summary>
    public Guid Id { get; init; }
}
