using App.Api.Authorization;
using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.TaxRates;
using App.Core.Features.TaxRates.CreateTaxRate;
using App.Core.Features.TaxRates.DeleteTaxRate;
using App.Core.Features.TaxRates.GetTaxRateById;
using App.Core.Features.TaxRates.GetTaxRates;
using App.Core.Features.TaxRates.UpdateTaxRate;
using App.Core.Features.TaxRates.UpdateTaxRateStatus;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 税率接口（需登录 + `taxRates.*` 权限点）：税率字典维护（新增 / 编辑 / 启停 / 删除）
/// </summary>
[Authorize]
[ApiController]
[Route("api/tax-rates")]
public class TaxRatesController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化税率控制器
    /// </summary>
    public TaxRatesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 分页查询税率（支持编码 / 名称关键词与状态筛选）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<TaxRateListItemDto>>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpGet]
    [RequirePermission(Permissions.TaxRatesView)]
    public async Task<ApiResponse<PagedResult<TaxRateListItemDto>>> GetTaxRates(
        [FromQuery] GetTaxRatesRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 新增税率（编码与名称全局唯一）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<TaxRateDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpPost]
    [RequirePermission(Permissions.TaxRatesCreate)]
    public async Task<ApiResponse<TaxRateDetailDto>> CreateTaxRate(
        [FromBody] CreateTaxRateRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 查询税率详情
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<TaxRateDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.TaxRatesView)]
    public async Task<ApiResponse<TaxRateDetailDto>> GetTaxRateById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetTaxRateByIdRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 编辑税率（编码 / 名称 / 税率 / 状态 / 备注全量覆盖）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<TaxRateDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.TaxRatesUpdate)]
    public async Task<ApiResponse<TaxRateDetailDto>> UpdateTaxRate(
        [FromRoute] Guid id,
        [FromBody] UpdateTaxRateRequest request,
        CancellationToken cancellationToken)
    {
        // 以路由 id 为准，避免请求体中的 id 覆盖路由
        var command = new UpdateTaxRateRequest
        {
            Id = id,
            Code = request.Code,
            Name = request.Name,
            Rate = request.Rate,
            Status = request.Status,
            Remark = request.Remark,
        };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// 删除税率
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<object?>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.TaxRatesDelete)]
    public async Task<ApiResponse<object?>> DeleteTaxRate(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new DeleteTaxRateRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 启用 / 停用税率（停用税率不可被新发票 / 凭证引用）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<TaxRateDetailDto>))]
    [ProducesResponseType(statusCode: StatusCodes.Status403Forbidden)]
    [HttpPut("{id:guid}/status")]
    [RequirePermission(Permissions.TaxRatesStatus)]
    public async Task<ApiResponse<TaxRateDetailDto>> UpdateTaxRateStatus(
        [FromRoute] Guid id,
        [FromBody] UpdateTaxRateStatusRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateTaxRateStatusRequest { Id = id, Status = request.Status };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }
}