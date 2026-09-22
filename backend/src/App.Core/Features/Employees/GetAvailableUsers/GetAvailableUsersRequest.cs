using App.Core.Abstractions;

namespace App.Core.Features.Employees.GetAvailableUsers;

/// <summary>
/// 员工可选账号查询请求（员工表单「关联账号」下拉数据源）
/// </summary>
public sealed class GetAvailableUsersRequest : IRequest<IReadOnlyList<EmployeePickUserDto>>
{
    /// <summary>当前员工 id，可空（新增场景不传；编辑场景传自身，放行其已绑定的账号）</summary>
    public Guid? EmployeeId { get; init; }
}
