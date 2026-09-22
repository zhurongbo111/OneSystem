using App.Core.Abstractions;

namespace App.Core.Features.Departments.GetDepartments;

/// <summary>
/// 部门树查询请求（空参数：组织量级小，一次返回全量树，不在服务端分页）
/// </summary>
public sealed class GetDepartmentsRequest : IRequest<IReadOnlyList<DepartmentTreeNodeDto>>
{
}
