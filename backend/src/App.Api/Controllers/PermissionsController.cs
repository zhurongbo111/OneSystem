using App.Api.Authorization;
using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.Permissions;
using App.Core.Features.Permissions.GetPermissions;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 权限点清单接口（需登录 + `roles.view`）：返回全部权限点分组，供角色授权界面渲染权限树
/// </summary>
[Authorize]
[ApiController]
[Route("api/permissions")]
public class PermissionsController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化权限点清单控制器
    /// </summary>
    public PermissionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 查询权限点分组清单（分组名 + key + 中文名，数据源为后端常量，前端不硬编码）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<IReadOnlyList<PermissionGroupDto>>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpGet]
    [RequirePermission(Permissions.RolesView)]
    public async Task<ApiResponse<IReadOnlyList<PermissionGroupDto>>> GetPermissions(CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetPermissionsRequest(), cancellationToken));
}
