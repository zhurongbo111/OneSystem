using App.Core.Abstractions;
using App.Core.Features.Costs;
using App.Core.Features.Costs.RecalculateCosts;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 成本接口（需登录）：成本重算 / 初始化（运维动作，按流水时序重放，幂等）
/// </summary>
[Authorize]
[ApiController]
[Route("api/costs")]
public class CostsController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化成本控制器
    /// </summary>
    /// <param name="mediator">用例中介</param>
    public CostsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 按流水时间顺序重算成本（可限定商品与期间；幂等，不改变任何库存数量）
    /// </summary>
    /// <param name="request">重算请求（Query 绑定，三个条件均可空）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<CostRecalculateResultDto>))]
    [HttpPost("recalculate")]
    public async Task<ApiResponse<CostRecalculateResultDto>> Recalculate(
        [FromQuery] RecalculateCostsRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));
}
