using App.Core.Abstractions;
using App.Core.Features.Products;
using App.Core.Features.Products.CreateProduct;
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
    [ProducesResponseType(401)]
    [HttpGet]
    public async Task<ApiResponse<PagedResult<ProductDto>>> GetProducts([FromQuery] GetProductsRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 开单商品选择（仅启用商品，全量返回；erp-purchase / erp-sale 开单页消费。
    /// 固定段 pick 置于 {id:guid} 之前注册，双保险）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<IReadOnlyList<ProductPickDto>>))]
    [ProducesResponseType(401)]
    [HttpGet("pick")]
    public async Task<ApiResponse<IReadOnlyList<ProductPickDto>>> GetProductPickList(CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetProductPickListRequest(), cancellationToken));

    /// <summary>
    /// 新增商品（同步初始化库存行 Quantity = 0，同一事务）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<ProductDto>))]
    [ProducesResponseType(401)]
    [HttpPost]
    public async Task<ApiResponse<ProductDto>> CreateProduct([FromBody] CreateProductRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 查询商品详情（含当前库存与分类名）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<ProductDto>))]
    [ProducesResponseType(401)]
    [HttpGet("{id:guid}")]
    public async Task<ApiResponse<ProductDto>> GetProductById([FromRoute] Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetProductByIdRequest { Id = id }, cancellationToken));

    /// <summary>
    /// 编辑商品（编码创建后不可修改，请求体不含 code）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<ProductDto>))]
    [ProducesResponseType(401)]
    [HttpPut("{id:guid}")]
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
    [ProducesResponseType(401)]
    [HttpPut("{id:guid}/status")]
    public async Task<ApiResponse<ProductDto>> UpdateProductStatus(
        [FromRoute] Guid id,
        [FromBody] UpdateProductStatusRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateProductStatusRequest { Id = id, Status = request.Status };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }
}
