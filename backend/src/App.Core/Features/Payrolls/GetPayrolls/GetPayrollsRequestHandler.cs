using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Responses;

namespace App.Core.Features.Payrolls.GetPayrolls;

/// <summary>
/// 工资单分页列表用例：按期间 / 员工 / 状态筛选后分页查询，直接依赖仓储，无 Service 层
/// </summary>
public sealed class GetPayrollsRequestHandler : IRequestHandler<GetPayrollsRequest, PagedResult<PayrollListItemDto>>
{
    private readonly IPayrollRepository _payrollRepository;

    /// <summary>
    /// 初始化工资单分页列表用例处理器
    /// </summary>
    public GetPayrollsRequestHandler(IPayrollRepository payrollRepository)
    {
        _payrollRepository = payrollRepository;
    }

    /// <summary>
    /// 处理工资单分页列表请求
    /// </summary>
    /// <param name="request">列表请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<PayrollListItemDto>> HandleAsync(GetPayrollsRequest request, CancellationToken cancellationToken = default)
    {
        var status = request.Status is null ? (PayrollStatus?)null : (PayrollStatus)request.Status.Value;
        var (items, total) = await _payrollRepository.GetPagedAsync(
            request.Year,
            request.Month,
            request.EmployeeId,
            status,
            request.Page,
            request.PageSize,
            cancellationToken);

        return new PagedResult<PayrollListItemDto>
        {
            Items = items.Select(PayrollDtoMapper.ToPayrollListItemDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
