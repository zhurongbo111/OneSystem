using App.Api.Authorization;
using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.Positions;
using App.Core.Features.Positions.CreatePosition;
using App.Core.Features.Positions.DeletePosition;
using App.Core.Features.Positions.GetPositionById;
using App.Core.Features.Positions.GetPositionPicks;
using App.Core.Features.Positions.GetPositions;
using App.Core.Features.Positions.UpdatePosition;
using App.Core.Features.Positions.UpdatePositionStatus;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 岗位接口（需登录 + `positions.*` 权限点）：岗位字典维护（新增 / 编辑 / 启停 / 删除）
/// </summary>
[Authorize]
[ApiController]
[Route("api/positions")]
public class PositionsController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化岗位控制器
    /// </summary>
    public PositionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 分页查询岗位（支持编码 / 名称关键词与状态筛选）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<PositionListItemDto>>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpGet]
    [RequirePermission(Permissions.PositionsView)]
    public async Task<ApiResponse<PagedResult<PositionListItemDto>>> GetPositions(
        [FromQuery] GetPositionsRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 岗位选择（仅启用岗位，全量返回；员工表单下拉消费。
    /// 固定段 picks 置于 {id:guid} 之前注册，双保险）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<IReadOnlyList<PositionPickDto>>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpGet("picks")]
    [RequirePermission(Permissions.PositionsView)]
    public async Task<ApiResponse<IReadOnlyList<PositionPickDto>>> GetPositionPicks(CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetPositionPicksRequest(), cancellationToken));

    /// <summary>
    /// 新增岗位（编码与名称全局唯一）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PositionDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpPost]
    [RequirePermission(Permissions.PositionsCreate)]
    public async Task<ApiResponse<PositionDetailDto>> CreatePosition(
        [FromBody] CreatePositionRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 查询岗位详情
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PositionDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.PositionsView)]
    public async Task<ApiResponse<PositionDetailDto>> GetPositionById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetPositionByIdRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 编辑岗位（编码 / 名称 / 状态 / 备注全量覆盖）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PositionDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.PositionsUpdate)]
    public async Task<ApiResponse<PositionDetailDto>> UpdatePosition(
        [FromRoute] Guid id,
        [FromBody] UpdatePositionRequest request,
        CancellationToken cancellationToken)
    {
        // 以路由 id 为准，避免请求体中的 id 覆盖路由
        var command = new UpdatePositionRequest
        {
            Id = id,
            Code = request.Code,
            Name = request.Name,
            Status = request.Status,
            Remark = request.Remark,
        };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// 删除岗位（已被员工引用时禁止删除）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<object?>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.PositionsDelete)]
    public async Task<ApiResponse<object?>> DeletePosition(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new DeletePositionRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 启用 / 停用岗位（停用后不参与员工选择）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PositionDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpPut("{id:guid}/status")]
    [RequirePermission(Permissions.PositionsStatus)]
    public async Task<ApiResponse<PositionDetailDto>> UpdatePositionStatus(
        [FromRoute] Guid id,
        [FromBody] UpdatePositionStatusRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdatePositionStatusRequest { Id = id, Status = request.Status };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }
}
