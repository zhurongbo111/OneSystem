using App.Core.Abstractions;

namespace App.Core.Features.Employees.UpdateEmployeeStatus;

/// <summary>
/// 员工在职 / 离职切换请求（离职不删除记录，保留历史与审计引用）
/// </summary>
public sealed class UpdateEmployeeStatusRequest : IRequest<EmployeeDetailDto>
{
    /// <summary>员工 id</summary>
    public Guid Id { get; init; }

    /// <summary>目标状态（0 离职 / 1 在职）</summary>
    public int Status { get; init; }
}
