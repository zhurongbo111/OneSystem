using App.Api.Authorization;
using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.Attendances;
using App.Core.Features.Attendances.CreateAttendance;
using App.Core.Features.Attendances.DeleteAttendance;
using App.Core.Features.Attendances.GetAttendances;
using App.Core.Features.Attendances.UpdateAttendance;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 考勤登记接口（需登录 + `attendance.*` 权限点）：请假 / 加班记录的登记与查询。
/// 只做登记（无审批、无打卡）；员工主体复用 `030` 的员工档案。
/// </summary>
[Authorize]
[ApiController]
[Route("api/attendances")]
public class AttendancesController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化考勤登记控制器
    /// </summary>
    public AttendancesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 分页查询考勤记录（支持员工 / 类型 / 日期范围筛选；日期范围按区间重叠判定）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<AttendanceListItemDto>>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpGet]
    [RequirePermission(Permissions.AttendanceView)]
    public async Task<ApiResponse<PagedResult<AttendanceListItemDto>>> GetAttendances(
        [FromQuery] GetAttendancesRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 新增考勤登记（员工须在职；同一员工同一类型的日期区间不可重叠）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<AttendanceDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpPost]
    [RequirePermission(Permissions.AttendanceCreate)]
    public async Task<ApiResponse<AttendanceDetailDto>> CreateAttendance(
        [FromBody] CreateAttendanceRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 编辑考勤登记（全量覆盖；员工可改，改后姓名快照随之刷新）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<AttendanceDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.AttendanceUpdate)]
    public async Task<ApiResponse<AttendanceDetailDto>> UpdateAttendance(
        [FromRoute] Guid id,
        [FromBody] UpdateAttendanceRequest request,
        CancellationToken cancellationToken)
    {
        // 以路由 id 为准，避免请求体中的 id 覆盖路由
        var command = new UpdateAttendanceRequest
        {
            Id = id,
            EmployeeId = request.EmployeeId,
            Type = request.Type,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Remark = request.Remark,
        };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// 删除考勤登记（考勤登记无状态，可直接删除）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<object>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.AttendanceDelete)]
    public async Task<ApiResponse<object?>> DeleteAttendance([FromRoute] Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new DeleteAttendanceRequest { Id = id }, cancellationToken));
}
