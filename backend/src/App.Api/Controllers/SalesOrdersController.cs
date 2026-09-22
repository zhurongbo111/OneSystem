using App.Api.Authorization;
using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.SalesOrders;
using App.Core.Features.SalesOrders.CloseSalesOrder;
using App.Core.Features.SalesOrders.CreateSalesOrder;
using App.Core.Features.SalesOrders.GetSalesOrderById;
using App.Core.Features.SalesOrders.GetSalesOrders;
using App.Core.Features.SalesOrders.UpdateSalesOrder;
using App.Core.Features.SalesOrders.VoidSalesOrder;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 销售订单控制器（/api/sales-orders）：列表分页 / 详情 / 新增 / 编辑（仅待发货）/ 作废 / 关闭。
/// 订单是计划单据：不动库存、不写库存流水（specs/024-erp-order-flow design.md §1）；
/// 发货由销售出库单（/api/sales-shipments）关联订单完成。
/// 统一 ApiResponse 包装，写接口强制鉴权。
/// </summary>
[Authorize]
[ApiController]
[Route("api/sales-orders")]
public sealed class SalesOrdersController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化销售订单控制器
    /// </summary>
    public SalesOrdersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 分页查询销售订单（含作废订单，作废 / 已关闭行前端置灰；含未发数量）
    /// </summary>
    /// <param name="request">分页查询请求（Query 绑定）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<SalesOrderListItemDto>>))]
    [HttpGet]
    [RequirePermission(Permissions.SalesOrdersView)]
    public async Task<ApiResponse<PagedResult<SalesOrderListItemDto>>> GetPaged([FromQuery] GetSalesOrdersRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 查询销售订单详情（含明细的订购 / 已发 / 未发数量）
    /// </summary>
    /// <param name="id">订单 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<SalesOrderDetailDto>))]
    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.SalesOrdersView)]
    public async Task<ApiResponse<SalesOrderDetailDto>> GetDetail(Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetSalesOrderByIdRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 新增销售订单（计划单据：不动库存、不写流水）
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<SalesOrderDetailDto>))]
    [HttpPost]
    [RequirePermission(Permissions.SalesOrdersCreate)]
    public async Task<ApiResponse<SalesOrderDetailDto>> Create([FromBody] CreateSalesOrderRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 编辑销售订单（仅「待发货」状态允许；明细整体替换）
    /// </summary>
    /// <param name="id">订单 id</param>
    /// <param name="request">编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<SalesOrderDetailDto>))]
    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.SalesOrdersUpdate)]
    public async Task<ApiResponse<SalesOrderDetailDto>> Update(Guid id, [FromBody] UpdateSalesOrderRequest request, CancellationToken cancellationToken)
    {
        // 以路由 id 为准，避免请求体中的 id 覆盖路由
        var command = new UpdateSalesOrderRequest
        {
            Id = id,
            PartnerId = request.PartnerId,
            OrderDate = request.OrderDate,
            ExpectedDate = request.ExpectedDate,
            Items = request.Items,
            Remark = request.Remark,
        };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// 作废销售订单（仅「待发货」状态允许；仅改状态不删数据）
    /// </summary>
    /// <param name="id">订单 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<SalesOrderDetailDto>))]
    [HttpPut("{id:guid}/void")]
    [RequirePermission(Permissions.SalesOrdersVoid)]
    public async Task<ApiResponse<SalesOrderDetailDto>> Void(Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new VoidSalesOrderRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 关闭销售订单（待发货 / 部分发货允许；剩余不再发货）
    /// </summary>
    /// <param name="id">订单 id</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<SalesOrderDetailDto>))]
    [HttpPut("{id:guid}/close")]
    [RequirePermission(Permissions.SalesOrdersClose)]
    public async Task<ApiResponse<SalesOrderDetailDto>> Close(Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new CloseSalesOrderRequest { Id = id }, cancellationToken));
}
