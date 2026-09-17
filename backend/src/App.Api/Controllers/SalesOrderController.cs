using App.Core.Abstractions;
using App.Core.Features.Sales;
using App.Core.Features.Sales.CreateSalesOrder;
using App.Core.Features.Sales.GetSalesOrderById;
using App.Core.Features.Sales.GetSalesOrders;
using App.Core.Features.Sales.VoidSalesOrder;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 销售单控制器（/api/sales-orders）：列表分页 / 详情 / 新增（保存即生效，库存减少）/ 作废回冲。
/// 结算金额由收付款单核销驱动（POST /api/settlements），本控制器不再提供手工结算切换（specs/023-erp-settlement）。
/// 写接口强制鉴权（design.md §3.3）；统一 ApiResponse 包装。
/// </summary>
[Authorize]
[ApiController]
[Route("api/sales-orders")]
public sealed class SalesOrderController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化销售单控制器
    /// </summary>
    public SalesOrderController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 分页查询销售单（含作废单据，作废行前端置灰）
    /// </summary>
    /// <param name="request">分页查询请求（Query 绑定）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<SalesOrderListItemDto>>))]
    [HttpGet]
    public async Task<ApiResponse<PagedResult<SalesOrderListItemDto>>> GetPaged([FromQuery] GetSalesOrdersRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 查询销售单详情（含明细行，快照字段原样返回）
    /// </summary>
    /// <param name="id">销售单 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<SalesOrderDetailDto>))]
    [HttpGet("{id:guid}")]
    public async Task<ApiResponse<SalesOrderDetailDto>> GetDetail(Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetSalesOrderByIdRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 新增销售单（一步式：保存即生效，库存立即减少；任一行库存不足整单拒绝 40103）
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<SalesOrderDetailDto>))]
    [HttpPost]
    public async Task<ApiResponse<SalesOrderDetailDto>> Create([FromBody] CreateSalesOrderRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 作废销售单（回冲库存；仅改状态不删数据）
    /// </summary>
    /// <param name="id">销售单 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<SalesOrderDetailDto>))]
    [HttpPut("{id:guid}/void")]
    public async Task<ApiResponse<SalesOrderDetailDto>> Void(Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new VoidSalesOrderRequest { Id = id }, cancellationToken));

}
