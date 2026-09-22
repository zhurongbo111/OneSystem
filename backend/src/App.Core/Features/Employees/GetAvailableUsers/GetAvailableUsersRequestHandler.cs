using App.Core.Abstractions;

namespace App.Core.Features.Employees.GetAvailableUsers;

/// <summary>
/// 员工可选账号用例：启用且未被任何员工绑定的账号 ∪ 当前员工已绑定的账号（仓储实现），直接依赖仓储
/// </summary>
public sealed class GetAvailableUsersRequestHandler : IRequestHandler<GetAvailableUsersRequest, IReadOnlyList<EmployeePickUserDto>>
{
    private readonly IEmployeeRepository _employeeRepository;

    /// <summary>
    /// 初始化员工可选账号用例处理器
    /// </summary>
    public GetAvailableUsersRequestHandler(IEmployeeRepository employeeRepository)
    {
        _employeeRepository = employeeRepository;
    }

    /// <summary>
    /// 处理员工可选账号请求
    /// </summary>
    /// <param name="request">查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<IReadOnlyList<EmployeePickUserDto>> HandleAsync(GetAvailableUsersRequest request, CancellationToken cancellationToken = default)
    {
        var items = await _employeeRepository.GetAvailableUsersAsync(request.EmployeeId, cancellationToken);
        return items.Select(EmployeeDtoMapper.ToEmployeePickUserDto).ToList();
    }
}
