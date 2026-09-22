using App.Api.Authorization;
using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.Roles;
using App.Core.Features.Roles.CreateRole;
using App.Core.Features.Roles.DeleteRole;
using App.Core.Features.Roles.GetRoleById;
using App.Core.Features.Roles.GetRoles;
using App.Core.Features.Roles.UpdateRole;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 角色接口（需登录 + `roles.*` 权限点）：角色维护（新增 / 编辑 / 删除）与列表查询
/// </summary>
[Authorize]
[ApiController]
[Route("api/roles")]
public class RolesController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化角色控制器
    /// </summary>
    public RolesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 分页查询角色（支持名称 / 备注关键词筛选）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<RoleListItemDto>>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpGet]
    [RequirePermission(Permissions.RolesView)]
    public async Task<ApiResponse<PagedResult<RoleListItemDto>>> GetRoles(
        [FromQuery] GetRolesRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 新增角色（含权限点集合）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<RoleDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpPost]
    [RequirePermission(Permissions.RolesCreate)]
    public async Task<ApiResponse<RoleDetailDto>> CreateRole(
        [FromBody] CreateRoleRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 查询角色详情（含已勾选权限点与绑定用户数）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<RoleDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.RolesView)]
    public async Task<ApiResponse<RoleDetailDto>> GetRoleById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetRoleByIdRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 编辑角色（名称 / 备注 / 权限点全量覆盖；内置 SuperAdmin 不可编辑）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<RoleDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.RolesUpdate)]
    public async Task<ApiResponse<RoleDetailDto>> UpdateRole(
        [FromRoute] Guid id,
        [FromBody] UpdateRoleRequest request,
        CancellationToken cancellationToken)
    {
        // 以路由 id 为准，避免请求体中的 id 覆盖路由
        var command = new UpdateRoleRequest
        {
            Id = id,
            Name = request.Name,
            Remark = request.Remark,
            PermissionKeys = request.PermissionKeys,
        };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// 删除角色（内置角色与已绑定用户的角色不可删除）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<object?>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.RolesDelete)]
    public async Task<ApiResponse<object?>> DeleteRole(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new DeleteRoleRequest { Id = id }, cancellationToken));
}
