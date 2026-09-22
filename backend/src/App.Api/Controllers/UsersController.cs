using App.Api.Authorization;
using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.Users;
using App.Core.Features.Users.CreateUser;
using App.Core.Features.Users.GetCurrentUser;
using App.Core.Features.Users.GetMyPermissions;
using App.Core.Features.Users.GetUserById;
using App.Core.Features.Users.GetUsers;
using App.Core.Features.Users.ResetPassword;
using App.Core.Features.Users.UpdateUser;
using App.Core.Features.Users.UpdateUserStatus;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 用户接口（需登录）：管理员在后台维护用户，系统不提供注册入口
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
    /// 获取当前登录用户（未登录实际返回 HTTP 200 + code 40100）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<UserDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status401Unauthorized)]
    [HttpGet("me")]
    [SkipPermissionCheck]
    public async Task<ApiResponse<UserDto>> Me(CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetCurrentUserRequest(), cancellationToken));

    /// <summary>
    /// 查询当前登录用户的权限点集合（免权限校验，否则会形成「需要权限才能查权限」死锁）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<IReadOnlyList<string>>))]
    [HttpGet("me/permissions")]
    [SkipPermissionCheck]
    public async Task<ApiResponse<IReadOnlyList<string>>> MyPermissions(CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetMyPermissionsRequest(), cancellationToken));

    /// <summary>
    /// 分页查询用户（支持关键词与状态筛选）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<UserListItemDto>>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpGet]
    [RequirePermission(Permissions.UsersView)]
    public async Task<ApiResponse<PagedResult<UserListItemDto>>> GetUsers([FromQuery] GetUsersRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 新增用户（管理员设置初始密码并绑定角色）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<UserDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpPost]
    [RequirePermission(Permissions.UsersCreate)]
    public async Task<ApiResponse<UserDetailDto>> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 查询用户详情
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<UserDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.UsersView)]
    public async Task<ApiResponse<UserDetailDto>> GetUserById([FromRoute] Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetUserByIdRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 编辑用户（用户名创建后不可修改；角色集合全量覆盖）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<UserDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.UsersUpdate)]
    public async Task<ApiResponse<UserDetailDto>> UpdateUser(
        [FromRoute] Guid id,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        // 以路由 id 为准，避免请求体中的 id 覆盖路由
        var command = new UpdateUserRequest
        {
            Id = id,
            DisplayName = request.DisplayName,
            Email = request.Email,
            Phone = request.Phone,
            RoleIds = request.RoleIds,
        };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// 启用 / 禁用用户（禁用后不能登录；不能禁用当前登录账号）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<UserDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpPut("{id:guid}/status")]
    [RequirePermission(Permissions.UsersStatus)]
    public async Task<ApiResponse<UserDetailDto>> UpdateUserStatus(
        [FromRoute] Guid id,
        [FromBody] UpdateUserStatusRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateUserStatusRequest { Id = id, Status = request.Status };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// 重置用户密码（管理员操作，无需原密码）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<object>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpPut("{id:guid}/password")]
    [RequirePermission(Permissions.UsersResetPassword)]
    public async Task<ApiResponse<object?>> ResetPassword(
        [FromRoute] Guid id,
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var command = new ResetPasswordRequest { Id = id, NewPassword = request.NewPassword };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }
}
