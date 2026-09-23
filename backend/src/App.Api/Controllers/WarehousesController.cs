using App.Api.Authorization;

using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.Warehouses;
using App.Core.Features.Warehouses.CreateWarehouse;
using App.Core.Features.Warehouses.GetWarehouseById;
using App.Core.Features.Warehouses.GetWarehousePickList;
using App.Core.Features.Warehouses.GetWarehouses;
using App.Core.Features.Warehouses.SetDefaultWarehouse;
using App.Core.Features.Warehouses.UpdateWarehouse;
using App.Core.Features.Warehouses.UpdateWarehouseStatus;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 仓库接口（需登录）：库存按仓的档案数据源，开单页仓库下拉亦取自本域（specs/038-erp-multi-warehouse）
/// </summary>
[Authorize]
[ApiController]
[Route("api/warehouses")]
public class WarehousesController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化仓库控制器
    /// </summary>
    public WarehousesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 分页查询仓库（关键词 / 状态筛选，默认仓置顶）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<WarehouseDto>>))]
    [HttpGet]
    [RequirePermission(Permissions.WarehousesView)]
    public async Task<ApiResponse<PagedResult<WarehouseDto>>> GetWarehouses(
        [FromQuery] GetWarehousesRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 新增仓库（默认启用、非默认仓）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<WarehouseDto>))]
    [HttpPost]
    [RequirePermission(Permissions.WarehousesCreate)]
    public async Task<ApiResponse<WarehouseDto>> CreateWarehouse(
        [FromBody] CreateWarehouseRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 查询启用仓库下拉（开单页数据源；固定段置于 <c>{id:guid}</c> 之前）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<IReadOnlyList<WarehousePickDto>>))]
    [HttpGet("pick")]
    [RequirePermission(Permissions.WarehousesView)]
    public async Task<ApiResponse<IReadOnlyList<WarehousePickDto>>> GetWarehousePickList(CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetWarehousePickListRequest(), cancellationToken));

    /// <summary>
    /// 查询仓库详情
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<WarehouseDto>))]
    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.WarehousesView)]
    public async Task<ApiResponse<WarehouseDto>> GetWarehouseById(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetWarehouseByIdRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 编辑仓库（编码创建后不可修改，请求体不含 code）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<WarehouseDto>))]
    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.WarehousesUpdate)]
    public async Task<ApiResponse<WarehouseDto>> UpdateWarehouse(
        [FromRoute] Guid id,
        [FromBody] UpdateWarehouseRequest request,
        CancellationToken cancellationToken)
    {
        // 以路由 id 为准，避免请求体中的 id 覆盖路由
        var command = new UpdateWarehouseRequest
        {
            Id = id,
            Name = request.Name,
            Address = request.Address,
            Contact = request.Contact,
            Phone = request.Phone,
            Remark = request.Remark,
        };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// 停用 / 启用仓库（默认仓不可停用，返回 40124）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<WarehouseDto>))]
    [HttpPut("{id:guid}/status")]
    [RequirePermission(Permissions.WarehousesStatus)]
    public async Task<ApiResponse<WarehouseDto>> UpdateWarehouseStatus(
        [FromRoute] Guid id,
        [FromBody] UpdateWarehouseStatusRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateWarehouseStatusRequest { Id = id, Status = request.Status };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// 设为默认仓（全系统唯一；同一事务内清空其他仓默认标记）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<WarehouseDto>))]
    [HttpPut("{id:guid}/default")]
    [RequirePermission(Permissions.WarehousesUpdate)]
    public async Task<ApiResponse<WarehouseDto>> SetDefaultWarehouse(
        [FromRoute] Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new SetDefaultWarehouseRequest { Id = id }, cancellationToken));
}
