using App.Core.Abstractions;
using App.Core.Features.Inventory;
using App.Core.Features.Inventory.GetInventory;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 库存查询接口（需登录）：只读列表，库存写入由采购 / 销售单据驱动
/// </summary>
[Authorize]
[ApiController]
[Route("api/inventory")]
public class InventoryController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化库存查询控制器
    /// </summary>
    public InventoryController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 分页查询库存（关键词 / 分类筛选，仅启用商品，按编码升序）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<InventoryItemDto>>))]
    [HttpGet]
    public async Task<ApiResponse<PagedResult<InventoryItemDto>>> GetInventory([FromQuery] GetInventoryRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));
}
