using App.Api.Authorization;
using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.LoginLogs;
using App.Core.Features.LoginLogs.GetLoginLogs;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 用户登录日志接口（需登录）：只读查询，日志只追加不修改
/// </summary>
[Authorize]
[ApiController]
[Route("api/login-logs")]
public class LoginLogsController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化登录日志控制器
    /// </summary>
    public LoginLogsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 分页查询登录日志（支持登录名与登录时间范围筛选，登录时间倒序）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<LoginLogListItemDto>>))]
    [HttpGet]
    [RequirePermission(Permissions.LoginLogsView)]
    public async Task<ApiResponse<PagedResult<LoginLogListItemDto>>> GetLoginLogs(
        [FromQuery] GetLoginLogsRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));
}
