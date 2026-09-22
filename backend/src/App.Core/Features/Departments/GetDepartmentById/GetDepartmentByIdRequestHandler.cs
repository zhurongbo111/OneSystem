using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.Departments.GetDepartmentById;

/// <summary>
/// 部门详情用例：按 id 查询，不存在返回 40400
/// </summary>
public sealed class GetDepartmentByIdRequestHandler : IRequestHandler<GetDepartmentByIdRequest, DepartmentDetailDto>
{
    private readonly IDepartmentRepository _departmentRepository;

    /// <summary>
    /// 初始化部门详情用例处理器
    /// </summary>
    public GetDepartmentByIdRequestHandler(IDepartmentRepository departmentRepository)
    {
        _departmentRepository = departmentRepository;
    }

    /// <summary>
    /// 处理部门详情请求
    /// </summary>
    /// <param name="request">详情请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<DepartmentDetailDto> HandleAsync(GetDepartmentByIdRequest request, CancellationToken cancellationToken = default)
    {
        var department = await _departmentRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "部门不存在");

        return DepartmentDtoMapper.ToDepartmentDetailDto(department);
    }
}
