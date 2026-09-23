using App.Api.Authorization;
using App.Api.Http;
using App.Core.Abstractions;
using App.Core.Auth;
using App.Core.Features.Products;
using App.Core.Features.Products.CreateProduct;
using App.Core.Features.Products.ExportProducts;
using App.Core.Features.Products.GetProductById;
using App.Core.Features.Products.GetProductPickList;
using App.Core.Features.Products.GetProducts;
using App.Core.Features.Products.UpdateProduct;
using App.Core.Features.Products.UpdateProductStatus;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 商品接口（需登录）：商品档案 + 库存列展示 + 开单商品选择
/// </summary>
[Authorize]
[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化商品控制器
    /// </summary>
    public ProductsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 分页查询商品（支持关键词 / 分类 / 状态筛选，列表含当前库存与低库存标记）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<ProductDto>>))]
    [HttpGet]
    [RequirePermission(Permissions.ProductsView)]
    public async Task<ApiResponse<PagedResult<ProductDto>>> GetProducts([FromQuery] GetProductsRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 开单商品选择（仅启用商品，全量返回；erp-purchase / erp-sale 开单页消费。
    /// 固定段 pick 置于 {id:guid} 之前注册，双保险）
    /// </summary>
    /// <param name="warehouseId">仓库 id，可空（038：传仓则库存为该仓数量，不传为各仓合计）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<IReadOnlyList<ProductPickDto>>))]
    [HttpGet("pick")]
    [RequirePermission(Permissions.ProductsView)]
    public async Task<ApiResponse<IReadOnlyList<ProductPickDto>>> GetProductPickList(
        [FromQuery] Guid? warehouseId, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(
            new GetProductPickListRequest { WarehouseId = warehouseId }, cancellationToken));

    /// <summary>
    /// 导出商品列表为 xlsx（erp-export）：沿用列表筛选，导出当前筛选全量（不受分页限制）。
    /// 成功返回二进制文件流（契约例外，specs/027-erp-export/design.md §0.1）；参数非法 / 服务端异常仍返回统一响应 JSON。
    /// 固定段 export 置于 {id:guid} 之前注册
    /// </summary>
    /// <param name="request">导出请求（Query 绑定，筛选参数同列表）</param>
    /// <param name="cancellationToken">取消令牌</param>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(FileResult))]
    [HttpGet("export")]
    [RequirePermission(Permissions.ProductsExport)]
    public async Task<IActionResult> Export([FromQuery] ExportProductsRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(request, cancellationToken);
        return File(result.Content, ExportFileTypes.Xlsx, result.FileName);
    }

    /// <summary>
    /// 新增商品（同步初始化库存行 Quantity = 0，同一事务）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<ProductDto>))]

    [HttpPost]
    [RequirePermission(Permissions.ProductsCreate)]
    public async Task<ApiResponse<ProductDto>> CreateProduct([FromBody] CreateProductRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 查询商品详情（含当前库存与分类名）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<ProductDto>))]

    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.ProductsView)]
    public async Task<ApiResponse<ProductDto>> GetProductById([FromRoute] Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetProductByIdRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 编辑商品（编码创建后不可修改，请求体不含 code）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<ProductDto>))]

    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.ProductsUpdate)]
    public async Task<ApiResponse<ProductDto>> UpdateProduct(
        [FromRoute] Guid id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        // 以路由 id 为准，避免请求体中的 id 覆盖路由
        var command = new UpdateProductRequest
        {
            Id = id,
            Name = request.Name,
            CategoryId = request.CategoryId,
            Unit = request.Unit,
            PurchasePrice = request.PurchasePrice,
            SalePrice = request.SalePrice,
            SafetyStock = request.SafetyStock,
            Remark = request.Remark,
        };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// 停用 / 启用商品（停用后不可被新单据选择，保留历史数据）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<ProductDto>))]

    [HttpPut("{id:guid}/status")]
    [RequirePermission(Permissions.ProductsStatus)]
    public async Task<ApiResponse<ProductDto>> UpdateProductStatus(
        [FromRoute] Guid id,
        [FromBody] UpdateProductStatusRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateProductStatusRequest { Id = id, Status = request.Status };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }
}
