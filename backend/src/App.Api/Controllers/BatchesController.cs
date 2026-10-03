using App.Api.Authorization;

using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.Batches;
using App.Core.Features.Batches.CreateBatch;
using App.Core.Features.Batches.GetBatchById;
using App.Core.Features.Batches.GetBatches;
using App.Core.Features.Batches.GetBatchPickList;
using App.Core.Features.Batches.UpdateBatch;
using App.Core.Features.Batches.UpdateBatchStatus;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 批次管理接口（需登录，040-erp-batch-expiry）：批次档案 CRUD + 开单页批次下拉
/// </summary>
[Authorize]
[ApiController]
[Route("api/batches")]
public class BatchesController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化批次管理控制器
    /// </summary>
    public BatchesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 分页查询批次（批次号关键词 / 商品 / 状态 / 仅看近效期或过期筛选）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<BatchListItemDto>>))]
    [HttpGet]
    [RequirePermission(Permissions.BatchesView)]
    public async Task<ApiResponse<PagedResult<BatchListItemDto>>> GetBatches([FromQuery] GetBatchesRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 新增批次
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<BatchDetailDto>))]
    [HttpPost]
    [RequirePermission(Permissions.BatchesCreate)]
    public async Task<ApiResponse<BatchDetailDto>> CreateBatch([FromBody] CreateBatchRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 查询批次详情
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<BatchDetailDto>))]
    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.BatchesView)]
    public async Task<ApiResponse<BatchDetailDto>> GetBatchById([FromRoute] Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetBatchByIdRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 编辑批次（批次号创建后不可修改，请求体不含 batchNo）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<BatchDetailDto>))]
    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.BatchesUpdate)]
    public async Task<ApiResponse<BatchDetailDto>> UpdateBatch(
        [FromRoute] Guid id,
        [FromBody] UpdateBatchRequest request,
        CancellationToken cancellationToken)
    {
        // 以路由 id 为准，避免请求体中的 id 覆盖路由
        var command = new UpdateBatchRequest
        {
            Id = id,
            ProductionDate = request.ProductionDate,
            ExpiryDate = request.ExpiryDate,
            Remark = request.Remark,
        };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// 停用 / 启用批次（停用后不可用于新的出入库单，保留历史数据）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<BatchDetailDto>))]
    [HttpPut("{id:guid}/status")]
    [RequirePermission(Permissions.BatchesUpdate)]
    public async Task<ApiResponse<BatchDetailDto>> UpdateBatchStatus(
        [FromRoute] Guid id,
        [FromBody] UpdateBatchStatusRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateBatchStatusRequest { Id = id, Status = request.Status };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// 批次下拉查询（开单页批次选择控件；按商品 + 仓返回启用批次与该仓可用库存）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<IReadOnlyList<BatchPickDto>>))]
    [HttpGet("pick")]
    [RequirePermission(Permissions.BatchesView)]
    public async Task<ApiResponse<IReadOnlyList<BatchPickDto>>> GetBatchPickList([FromQuery] GetBatchPickListRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));
}
