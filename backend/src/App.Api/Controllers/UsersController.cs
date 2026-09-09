using App.Core.Abstractions;
using App.Core.Features.Users;
using App.Core.Features.Users.GetCurrentUser;
using App.Core.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 用户接口（需登录）
/// </summary>
[Authorize]
[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化用户控制器
    /// </summary>
    public UsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 获取当前登录用户
    /// </summary>
    [HttpGet("me")]
    public async Task<ApiResponse<UserDto>> Me(CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetCurrentUserRequest(), cancellationToken));
}
