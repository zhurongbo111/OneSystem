using App.Core.Abstractions;

namespace App.Core.Features.Departments.GetDepartmentById;

/// <summary>
/// 部门详情请求
/// </summary>
public sealed class GetDepartmentByIdRequest : IRequest<DepartmentDetailDto>
{
    /// <summary>部门 id</summary>
    public Guid Id { get; init; }
}
