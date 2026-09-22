using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.Employees.GetEmployeeById;

/// <summary>
/// 员工详情用例：按 id 查询（联查部门 / 岗位 / 关联账号名称），不存在返回 40400
/// </summary>
public sealed class GetEmployeeByIdRequestHandler : IRequestHandler<GetEmployeeByIdRequest, EmployeeDetailDto>
{
    private readonly IEmployeeRepository _employeeRepository;

    /// <summary>
    /// 初始化员工详情用例处理器
    /// </summary>
    public GetEmployeeByIdRequestHandler(IEmployeeRepository employeeRepository)
    {
        _employeeRepository = employeeRepository;
    }

    /// <summary>
    /// 处理员工详情请求
    /// </summary>
    /// <param name="request">详情请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<EmployeeDetailDto> HandleAsync(GetEmployeeByIdRequest request, CancellationToken cancellationToken = default)
    {
        var detail = await _employeeRepository.GetDetailAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "员工不存在");

        return EmployeeDtoMapper.ToEmployeeDetailDto(detail);
    }
}
