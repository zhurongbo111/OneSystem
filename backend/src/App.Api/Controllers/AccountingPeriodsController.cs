using App.Api.Authorization;
using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.AccountingPeriods.ClosePeriod;
using App.Core.Features.AccountingPeriods.GetPeriods;
using App.Core.Features.AccountingPeriods.ReversePeriod;
using App.Core.Features.GeneralLedger;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 会计期间接口（需登录 + `vouchers.*` 权限点）：期间列表 + 结账 / 反结账
/// </summary>
[Authorize]
[ApiController]
[Route("api/accounting-periods")]
public class AccountingPeriodsController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化会计期间控制器
    /// </summary>
    public AccountingPeriodsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 查询会计期间列表（可按年过滤；年不传返回全部）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<IReadOnlyList<PeriodDto>>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpGet]
    [RequirePermission(Permissions.VouchersView)]
    public async Task<ApiResponse<IReadOnlyList<PeriodDto>>> GetPeriods(
        [FromQuery] GetPeriodsRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 会计期间结账（已结账幂等返回；结账后该期间禁止新增 / 作废凭证）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PeriodDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpPut("{id:guid}/close")]
    [RequirePermission(Permissions.VouchersClose)]
    public async Task<ApiResponse<PeriodDto>> ClosePeriod(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new ClosePeriodRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 会计期间反结账（未结账幂等返回；反结账后可继续记账）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PeriodDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpPut("{id:guid}/reverse")]
    [RequirePermission(Permissions.VouchersClose)]
    public async Task<ApiResponse<PeriodDto>> ReversePeriod(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new ReversePeriodRequest { Id = id }, cancellationToken));
}
