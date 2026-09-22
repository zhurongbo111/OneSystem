using App.Api.Authorization;
using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.AuditLogs;
using App.Core.Features.AuditLogs.GetAuditLogById;
using App.Core.Features.AuditLogs.GetAuditLogs;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 业务操作审计日志接口（需登录 + `auditLogs.view` 权限点）：只读查询，日志纯追加、不提供改删接口
/// </summary>
[Authorize]
[ApiController]
[Route("api/audit-logs")]
public class AuditLogsController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化操作日志控制器
    /// </summary>
    public AuditLogsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 分页查询操作日志（支持关键词 / 资源类型 / 动作 / 操作人 / 时间范围筛选，操作时间倒序）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<AuditLogListItemDto>>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpGet]
    [RequirePermission(Permissions.AuditLogsView)]
    public async Task<ApiResponse<PagedResult<AuditLogListItemDto>>> GetAuditLogs(
        [FromQuery] GetAuditLogsRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 查询单条操作日志详情（含字段级变更前 / 后值）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<AuditLogDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.AuditLogsView)]
    public async Task<ApiResponse<AuditLogDetailDto>> GetAuditLogById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetAuditLogByIdRequest { Id = id }, cancellationToken));
}
