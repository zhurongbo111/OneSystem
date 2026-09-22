using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.Employees;

/// <summary>
/// 员工写用例的详情回读（新增 / 编辑 / 启停共用）：写操作落库后统一经读模型回读，
/// 保证写用例出参与详情出参口径一致（部门 / 岗位 / 关联账号名称由仓储联查带出）。
/// </summary>
internal static class EmployeeDetailLoader
{
    /// <summary>
    /// 按 id 回读员工详情并映射为出参；记录不存在时抛 40400（写后应必然存在，缺失说明数据异常）
    /// </summary>
    /// <param name="employeeRepository">员工仓储</param>
    /// <param name="employeeId">员工 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    public static async Task<EmployeeDetailDto> LoadAsync(
        IEmployeeRepository employeeRepository,
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        var detail = await employeeRepository.GetDetailAsync(employeeId, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "员工不存在");

        return EmployeeDtoMapper.ToEmployeeDetailDto(detail);
    }
}
