using App.Api.Authorization;
using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.Payrolls;
using App.Core.Features.Payrolls.CreatePayroll;
using App.Core.Features.Payrolls.DeletePayroll;
using App.Core.Features.Payrolls.GeneratePayrolls;
using App.Core.Features.Payrolls.GetPayrolls;
using App.Core.Features.Payrolls.UpdatePayroll;
using App.Core.Features.Payrolls.UpdatePayrollStatus;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 月度工资单接口（需登录 + `payroll.*` 权限点）：工资单维护、批量生成与发放。
/// 口径从简（基本工资 + 津贴 − 扣款，不做个税 / 社保）；实发一律由后端重算。
/// </summary>
[Authorize]
[ApiController]
[Route("api/payrolls")]
public class PayrollsController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化工资单控制器
    /// </summary>
    public PayrollsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 分页查询工资单（支持期间 / 员工 / 状态筛选）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<PayrollListItemDto>>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpGet]
    [RequirePermission(Permissions.PayrollView)]
    public async Task<ApiResponse<PagedResult<PayrollListItemDto>>> GetPayrolls(
        [FromQuery] GetPayrollsRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 新增工资单（一个员工一个月一条，重复返回 40170；实发由后端按口径计算）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PayrollDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpPost]
    [RequirePermission(Permissions.PayrollCreate)]
    public async Task<ApiResponse<PayrollDetailDto>> CreatePayroll(
        [FromBody] CreatePayrollRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 批量生成指定期间的工资单草稿（为在职员工生成；已存在则跳过，返回新增 / 跳过计数）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<GeneratePayrollsResponse>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpPost("generate")]
    [RequirePermission(Permissions.PayrollCreate)]
    public async Task<ApiResponse<GeneratePayrollsResponse>> GeneratePayrolls(
        [FromQuery] GeneratePayrollsRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 编辑工资单（仅草稿；已发放返回 40171。员工与期间不可改，不在请求体内）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PayrollDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.PayrollUpdate)]
    public async Task<ApiResponse<PayrollDetailDto>> UpdatePayroll(
        [FromRoute] Guid id,
        [FromBody] UpdatePayrollRequest request,
        CancellationToken cancellationToken)
    {
        // 以路由 id 为准，避免请求体中的 id 覆盖路由
        var command = new UpdatePayrollRequest
        {
            Id = id,
            BaseSalary = request.BaseSalary,
            Allowance = request.Allowance,
            Deduction = request.Deduction,
            Remark = request.Remark,
        };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// 发放 / 反发放工资单（草稿 ⇄ 已发放；发放后编辑 / 删除被锁）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PayrollDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpPut("{id:guid}/status")]
    [RequirePermission(Permissions.PayrollStatus)]
    public async Task<ApiResponse<PayrollDetailDto>> UpdatePayrollStatus(
        [FromRoute] Guid id,
        [FromBody] UpdatePayrollStatusRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdatePayrollStatusRequest { Id = id, Status = request.Status };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// 删除工资单（仅草稿；已发放返回 40171）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<object>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.PayrollDelete)]
    public async Task<ApiResponse<object?>> DeletePayroll([FromRoute] Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new DeletePayrollRequest { Id = id }, cancellationToken));
}
