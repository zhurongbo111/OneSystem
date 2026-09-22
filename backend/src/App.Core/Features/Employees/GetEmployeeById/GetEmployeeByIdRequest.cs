using App.Core.Abstractions;

namespace App.Core.Features.Employees.GetEmployeeById;

/// <summary>
/// 员工详情请求
/// </summary>
public sealed class GetEmployeeByIdRequest : IRequest<EmployeeDetailDto>
{
    /// <summary>员工 id</summary>
    public Guid Id { get; init; }
}
