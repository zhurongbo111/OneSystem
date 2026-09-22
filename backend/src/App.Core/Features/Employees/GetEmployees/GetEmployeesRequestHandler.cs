using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Responses;

namespace App.Core.Features.Employees.GetEmployees;

/// <summary>
/// 员工分页列表用例：按关键词 / 部门 / 岗位 / 状态筛选后分页查询（部门名 / 岗位名 / 关联账号显示名由仓储联查带出），
/// 直接依赖仓储，无 Service 层
/// </summary>
public sealed class GetEmployeesRequestHandler : IRequestHandler<GetEmployeesRequest, PagedResult<EmployeeListItemDto>>
{
    private readonly IEmployeeRepository _employeeRepository;

    /// <summary>
    /// 初始化员工分页列表用例处理器
    /// </summary>
    public GetEmployeesRequestHandler(IEmployeeRepository employeeRepository)
    {
        _employeeRepository = employeeRepository;
    }

    /// <summary>
    /// 处理员工分页列表请求
    /// </summary>
    /// <param name="request">列表请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<EmployeeListItemDto>> HandleAsync(GetEmployeesRequest request, CancellationToken cancellationToken = default)
    {
        var status = request.Status is null ? (EmployeeStatus?)null : (EmployeeStatus)request.Status.Value;
        var (items, total) = await _employeeRepository.GetPagedAsync(
            request.Keyword,
            request.DepartmentId,
            request.PositionId,
            status,
            request.Page,
            request.PageSize,
            cancellationToken);

        return new PagedResult<EmployeeListItemDto>
        {
            Items = items.Select(EmployeeDtoMapper.ToEmployeeListItemDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
