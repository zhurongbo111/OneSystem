using App.Api.Authorization;
using App.Api.Http;

using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.StockMovements;
using App.Core.Features.StockMovements.ExportStockMovements;
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
    [RequirePermission(Permissions.StockMovementsView)]
    public async Task<ApiResponse<PagedResult<StockMovementListItemDto>>> GetStockMovements(
        [FromQuery] GetStockMovementsRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 导出库存流水列表（Excel；取当前筛选全量，成功返回二进制文件流，契约例外见 specs/027-erp-export/design.md §0.1）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(FileResult))]
    [HttpGet("export")]
    [RequirePermission(Permissions.StockMovementsExport)]
    public async Task<IActionResult> Export([FromQuery] ExportStockMovementsRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(request, cancellationToken);
        return File(result.Content, ExportFileTypes.Xlsx, result.FileName);
    }
}
