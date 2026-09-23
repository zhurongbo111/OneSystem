using App.Api.Authorization;
using App.Api.Http;

using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.PartnerPrices;
using App.Core.Features.PartnerPrices.CreatePartnerPrice;
using App.Core.Features.PartnerPrices.DeletePartnerPrice;
using App.Core.Features.PartnerPrices.ExportPartnerPrices;
using App.Core.Features.PartnerPrices.GetEffectivePrices;
using App.Core.Features.PartnerPrices.GetPartnerPriceById;
using App.Core.Features.PartnerPrices.GetPartnerPrices;
using App.Core.Features.PartnerPrices.UpdatePartnerPrice;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 客户协议价接口（需登录）：客户 × 商品的协议单价，销售开单取价来源之一
/// </summary>
[Authorize]
[ApiController]
[Route("api/partner-prices")]
public class PartnerPricesController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化客户协议价控制器
    /// </summary>
    public PartnerPricesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 分页查询客户协议价（客户 / 商品 / 关键词筛选）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<PartnerPriceListItemDto>>))]
    [HttpGet]
    [RequirePermission(Permissions.PartnerPricesView)]
    public async Task<ApiResponse<PagedResult<PartnerPriceListItemDto>>> GetPartnerPrices(
        [FromQuery] GetPartnerPricesRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 新增客户协议价
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PartnerPriceDetailDto>))]
    [HttpPost]
    [RequirePermission(Permissions.PartnerPricesCreate)]
    public async Task<ApiResponse<PartnerPriceDetailDto>> CreatePartnerPrice(
        [FromBody] CreatePartnerPriceRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 批量取生效价（销售开单：按客户 + 商品集合一次取价，含来源标注）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<IReadOnlyList<EffectivePriceDto>>))]
    [HttpGet("effective")]
    [RequirePermission(Permissions.PartnerPricesView)]
    public async Task<ApiResponse<IReadOnlyList<EffectivePriceDto>>> GetEffectivePrices(
        [FromQuery] GetEffectivePricesRequest request,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 导出客户价格列表（Excel；取当前筛选全量，成功返回二进制文件流，契约例外见 specs/027-erp-export/design.md §0.1）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(FileResult))]
    [HttpGet("export")]
    [RequirePermission(Permissions.PartnerPricesExport)]
    public async Task<IActionResult> Export([FromQuery] ExportPartnerPricesRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(request, cancellationToken);
        return File(result.Content, ExportFileTypes.Xlsx, result.FileName);
    }

    /// <summary>
    /// 查询客户协议价详情
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PartnerPriceDetailDto>))]
    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.PartnerPricesView)]
    public async Task<ApiResponse<PartnerPriceDetailDto>> GetPartnerPriceById([FromRoute] Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetPartnerPriceByIdRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 编辑客户协议价（客户与商品不可改，请求体只含单价与备注）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PartnerPriceDetailDto>))]
    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.PartnerPricesUpdate)]
    public async Task<ApiResponse<PartnerPriceDetailDto>> UpdatePartnerPrice(
        [FromRoute] Guid id,
        [FromBody] UpdatePartnerPriceRequest request,
        CancellationToken cancellationToken)
    {
        // 以路由 id 为准，避免请求体中的 id 覆盖路由
        var command = new UpdatePartnerPriceRequest
        {
            Id = id,
            Price = request.Price,
            Remark = request.Remark,
        };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// 删除客户协议价（删除即回退为商品销售价）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<object?>))]
    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.PartnerPricesDelete)]
    public async Task<ApiResponse<object?>> DeletePartnerPrice([FromRoute] Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new DeletePartnerPriceRequest { Id = id }, cancellationToken));
}