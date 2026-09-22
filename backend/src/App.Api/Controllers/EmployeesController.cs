using App.Api.Authorization;
using App.Api.Http;
using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.Employees;
using App.Core.Features.Employees.CreateEmployee;
using App.Core.Features.Employees.ExportEmployees;
using App.Core.Features.Employees.GetAvailableUsers;
using App.Core.Features.Employees.GetEmployeeById;
using App.Core.Features.Employees.GetEmployees;
using App.Core.Features.Employees.UpdateEmployee;
using App.Core.Features.Employees.UpdateEmployeeStatus;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 员工档案接口（需登录 + `employees.*` 权限点）：员工维护（新增 / 编辑 / 在职离职切换）与列表 / 导出。
/// 员工不是账号：登录一律走用户域，本控制器只做「绑定」。
/// </summary>
[Authorize]
[ApiController]
[Route("api/employees")]
public class EmployeesController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化员工控制器
    /// </summary>
    public EmployeesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 分页查询员工（支持工号 / 姓名关键词、部门、岗位、在职状态筛选；出参含部门名 / 岗位名 / 关联账号显示名）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<EmployeeListItemDto>>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpGet]
    [RequirePermission(Permissions.EmployeesView)]
    public async Task<ApiResponse<PagedResult<EmployeeListItemDto>>> GetEmployees(
        [FromQuery] GetEmployeesRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 可选账号来源（启用且未被绑定的账号 ∪ 指定员工已绑定的账号；员工表单下拉消费。
    /// 固定段 available-users 置于 {id:guid} 之前注册）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<IReadOnlyList<EmployeePickUserDto>>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpGet("available-users")]
    [RequirePermission(Permissions.EmployeesView)]
    public async Task<ApiResponse<IReadOnlyList<EmployeePickUserDto>>> GetAvailableUsers(
        [FromQuery] GetAvailableUsersRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 导出员工档案为 xlsx（erp-export）：沿用列表筛选，导出当前筛选全量（不受分页限制）。
    /// 成功返回二进制文件流（契约例外，specs/027-erp-export/design.md §0.1）；参数非法 / 服务端异常仍返回统一响应 JSON。
    /// 固定段 export 置于 {id:guid} 之前注册
    /// </summary>
    /// <param name="request">导出请求（Query 绑定，筛选参数同列表）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(FileResult))]
    [HttpGet("export")]
    [RequirePermission(Permissions.EmployeesExport)]
    public async Task<IActionResult> Export([FromQuery] ExportEmployeesRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(request, cancellationToken);
        return File(result.Content, ExportFileTypes.Xlsx, result.FileName);
    }

    /// <summary>
    /// 新增员工（工号唯一且创建后不可改；手机 / 邮箱 / 关联账号非空时唯一）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<EmployeeDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpPost]
    [RequirePermission(Permissions.EmployeesCreate)]
    public async Task<ApiResponse<EmployeeDetailDto>> CreateEmployee(
        [FromBody] CreateEmployeeRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 查询员工详情（含部门 / 岗位 / 关联账号名称）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<EmployeeDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.EmployeesView)]
    public async Task<ApiResponse<EmployeeDetailDto>> GetEmployeeById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetEmployeeByIdRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 编辑员工（工号不含在请求体：创建后不可修改；其余字段全量覆盖）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<EmployeeDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.EmployeesUpdate)]
    public async Task<ApiResponse<EmployeeDetailDto>> UpdateEmployee(
        [FromRoute] Guid id,
        [FromBody] UpdateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        // 以路由 id 为准，避免请求体中的 id 覆盖路由；工号不可改，故不接收
        var command = new UpdateEmployeeRequest
        {
            Id = id,
            Name = request.Name,
            Gender = request.Gender,
            Phone = request.Phone,
            Email = request.Email,
            DepartmentId = request.DepartmentId,
            PositionId = request.PositionId,
            HireDate = request.HireDate,
            ResignDate = request.ResignDate,
            Status = request.Status,
            UserId = request.UserId,
            Remark = request.Remark,
        };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// 在职 / 离职切换（离职不删除记录；置离职时为空则补当天离职日期）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<EmployeeDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpPut("{id:guid}/status")]
    [RequirePermission(Permissions.EmployeesStatus)]
    public async Task<ApiResponse<EmployeeDetailDto>> UpdateEmployeeStatus(
        [FromRoute] Guid id,
        [FromBody] UpdateEmployeeStatusRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateEmployeeStatusRequest { Id = id, Status = request.Status };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }
}
