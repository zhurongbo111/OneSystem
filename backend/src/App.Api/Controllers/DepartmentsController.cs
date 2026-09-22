using App.Api.Authorization;
using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.Departments;
using App.Core.Features.Departments.CreateDepartment;
using App.Core.Features.Departments.DeleteDepartment;
using App.Core.Features.Departments.GetDepartmentById;
using App.Core.Features.Departments.GetDepartments;
using App.Core.Features.Departments.UpdateDepartment;
using App.Core.Features.Departments.UpdateDepartmentStatus;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 部门接口（需登录 + `departments.*` 权限点）：部门树维护（新增 / 编辑 / 启停 / 删除）
/// </summary>
[Authorize]
[ApiController]
[Route("api/departments")]
public class DepartmentsController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化部门控制器
    /// </summary>
    public DepartmentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 查询部门树（含各节点在职人数；组织量级小，一次返回全量树，不在服务端分页）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<IReadOnlyList<DepartmentTreeNodeDto>>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpGet]
    [RequirePermission(Permissions.DepartmentsView)]
    public async Task<ApiResponse<IReadOnlyList<DepartmentTreeNodeDto>>> GetDepartments(CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetDepartmentsRequest(), cancellationToken));

    /// <summary>
    /// 新增部门（编码全局唯一；同一上级下名称唯一；上级可空表示顶级）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<DepartmentDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpPost]
    [RequirePermission(Permissions.DepartmentsCreate)]
    public async Task<ApiResponse<DepartmentDetailDto>> CreateDepartment(
        [FromBody] CreateDepartmentRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 查询部门详情
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<DepartmentDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.DepartmentsView)]
    public async Task<ApiResponse<DepartmentDetailDto>> GetDepartmentById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetDepartmentByIdRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 编辑部门（编码 / 名称 / 上级 / 排序 / 状态 / 备注全量覆盖；上级不得为自身或自身后代）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<DepartmentDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.DepartmentsUpdate)]
    public async Task<ApiResponse<DepartmentDetailDto>> UpdateDepartment(
        [FromRoute] Guid id,
        [FromBody] UpdateDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        // 以路由 id 为准，避免请求体中的 id 覆盖路由
        var command = new UpdateDepartmentRequest
        {
            Id = id,
            Code = request.Code,
            Name = request.Name,
            ParentId = request.ParentId,
            SortOrder = request.SortOrder,
            Status = request.Status,
            Remark = request.Remark,
        };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// 删除部门（存在子部门或员工引用时禁止删除）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<object?>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.DepartmentsDelete)]
    public async Task<ApiResponse<object?>> DeleteDepartment(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new DeleteDepartmentRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 启用 / 停用部门（停用后不参与新增下级与员工选择）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<DepartmentDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpPut("{id:guid}/status")]
    [RequirePermission(Permissions.DepartmentsStatus)]
    public async Task<ApiResponse<DepartmentDetailDto>> UpdateDepartmentStatus(
        [FromRoute] Guid id,
        [FromBody] UpdateDepartmentStatusRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateDepartmentStatusRequest { Id = id, Status = request.Status };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }
}
