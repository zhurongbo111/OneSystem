using App.Api.Authorization;
using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.CashJournals;
using App.Core.Features.CashJournals.GetCashJournal;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 资金日记账接口（需登录 + `cashJournals.view` 权限点）：只读，由收付款单派生
/// </summary>
[Authorize]
[ApiController]
[Route("api/cash-journals")]
public class CashJournalsController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化资金日记账控制器
    /// </summary>
    public CashJournalsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 查询指定资金账户在日期区间内的资金日记账（期初 / 逐笔收付与结余 / 期末）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<CashJournalDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpGet]
    [RequirePermission(Permissions.CashJournalsView)]
    public async Task<ApiResponse<CashJournalDto>> GetCashJournal(
        [FromQuery] GetCashJournalRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));
}
