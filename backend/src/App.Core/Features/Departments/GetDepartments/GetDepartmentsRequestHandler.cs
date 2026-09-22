using App.Core.Abstractions;

namespace App.Core.Features.Departments.GetDepartments;

/// <summary>
/// 部门树用例：仓储一次取全量并组装为树（含各节点在职人数），直接依赖仓储，无 Service 层
/// </summary>
public sealed class GetDepartmentsRequestHandler : IRequestHandler<GetDepartmentsRequest, IReadOnlyList<DepartmentTreeNodeDto>>
{
    private readonly IDepartmentRepository _departmentRepository;

    /// <summary>
    /// 初始化部门树用例处理器
    /// </summary>
    public GetDepartmentsRequestHandler(IDepartmentRepository departmentRepository)
    {
        _departmentRepository = departmentRepository;
    }

    /// <summary>
    /// 处理部门树请求
    /// </summary>
    /// <param name="request">树请求（空参数）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<IReadOnlyList<DepartmentTreeNodeDto>> HandleAsync(GetDepartmentsRequest request, CancellationToken cancellationToken = default)
    {
        var roots = await _departmentRepository.GetTreeAsync(cancellationToken);
        return roots.Select(DepartmentDtoMapper.ToDepartmentTreeNodeDto).ToList();
    }
}
