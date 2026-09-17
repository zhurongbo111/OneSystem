using App.Core.Abstractions;
using App.Core.Features.StockMovements;
using App.Core.Features.StockMovements.GetStockMovements;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 库存变动流水接口（需登录）：只读查询，流水纯追加不修改
/// </summary>
[Authorize]
[ApiController]
[Route("api/stock-movements")]
public class StockMovementsController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化库存流水控制器
    /// </summary>
    public StockMovementsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 分页查询库存流水（支持单号关键词 / 商品 / 变动类型 / 变动时间范围筛选，变动时间倒序）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<StockMovementListItemDto>>))]
    [HttpGet]
    public async Task<ApiResponse<PagedResult<StockMovementListItemDto>>> GetStockMovements(
        [FromQuery] GetStockMovementsRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));
}
