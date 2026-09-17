using App.Core.Abstractions;
using App.Core.Features.StockTakes;
using App.Core.Features.StockTakes.CreateStockTake;
using App.Core.Features.StockTakes.GetStockTakeById;
using App.Core.Features.StockTakes.GetStockTakePickProducts;
using App.Core.Features.StockTakes.GetStockTakes;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 盘点 / 期初建账控制器（/api/stock-takes）：列表分页 / 商品选择 / 详情 / 新增（保存即生效）。
/// 写接口强制鉴权；统一 ApiResponse 包装。
/// 路由顺序：/pick-products 固定段注册在 {id:guid} 之前（{id:guid} 约束本身已排除 pick-products，双保险）。
/// </summary>
[Authorize]
[ApiController]
[Route("api/stock-takes")]
public sealed class StockTakesController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化盘点 / 期初建账控制器
    /// </summary>
    public StockTakesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 分页查询盘点单（单号关键词 / 类型 / 盘点日期范围筛选）
    /// </summary>
    /// <param name="request">分页查询请求（Query 绑定）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<StockTakeListItemDto>>))]
    [HttpGet]
    public async Task<ApiResponse<PagedResult<StockTakeListItemDto>>> GetPaged([FromQuery] GetStockTakesRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 盘点商品选择（启用商品 + 当前库存 + 是否已发生库存变动）。
    /// 固定段路由，注册在 {id:guid} 之前。
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<IReadOnlyList<StockTakeProductPickDto>>))]
    [HttpGet("pick-products")]
    public async Task<ApiResponse<IReadOnlyList<StockTakeProductPickDto>>> GetPickProducts(CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetStockTakePickProductsRequest(), cancellationToken));

    /// <summary>
    /// 查询盘点单详情（含明细行，快照字段原样返回）
    /// </summary>
    /// <param name="id">盘点单 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<StockTakeDetailDto>))]
    [HttpGet("{id:guid}")]
    public async Task<ApiResponse<StockTakeDetailDto>> GetDetail(Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetStockTakeByIdRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 新增盘点 / 期初建账单（一步式：保存即生效，库存按实盘数量设定 + 差异行写流水）
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<StockTakeDetailDto>))]
    [HttpPost]
    public async Task<ApiResponse<StockTakeDetailDto>> Create([FromBody] CreateStockTakeRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));
}
