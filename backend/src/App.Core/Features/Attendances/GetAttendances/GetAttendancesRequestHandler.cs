using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Responses;

namespace App.Core.Features.Attendances.GetAttendances;

/// <summary>
/// 考勤记录分页列表用例：按员工 / 类型 / 日期范围（区间重叠）筛选后分页查询，
/// 直接依赖仓储，无 Service 层
/// </summary>
public sealed class GetAttendancesRequestHandler : IRequestHandler<GetAttendancesRequest, PagedResult<AttendanceListItemDto>>
{
    private readonly IAttendanceRepository _attendanceRepository;

    /// <summary>
    /// 初始化考勤记录分页列表用例处理器
    /// </summary>
    public GetAttendancesRequestHandler(IAttendanceRepository attendanceRepository)
    {
        _attendanceRepository = attendanceRepository;
    }

    /// <summary>
    /// 处理考勤记录分页列表请求
    /// </summary>
    /// <param name="request">列表请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<AttendanceListItemDto>> HandleAsync(GetAttendancesRequest request, CancellationToken cancellationToken = default)
    {
        var type = request.Type is null ? (AttendanceType?)null : (AttendanceType)request.Type.Value;
        var (items, total) = await _attendanceRepository.GetPagedAsync(
            request.EmployeeId,
            type,
            request.StartDate,
            request.EndDate,
            request.Page,
            request.PageSize,
            cancellationToken);

        return new PagedResult<AttendanceListItemDto>
        {
            Items = items.Select(AttendanceDtoMapper.ToAttendanceListItemDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
