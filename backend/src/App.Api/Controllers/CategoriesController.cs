using App.Core.Abstractions;
using App.Core.Features.Categories;
using App.Core.Features.Categories.CreateCategory;
using App.Core.Features.Categories.DeleteCategory;
using App.Core.Features.Categories.GetCategories;
using App.Core.Features.Categories.GetCategoriesPaged;
using App.Core.Features.Categories.UpdateCategory;
using App.Core.Responses;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Controllers;

/// <summary>
/// 商品分类接口（需登录）：单级字典维护（列表 / 新增 / 编辑 / 删除）
/// </summary>
[Authorize]
[ApiController]
[Route("api/categories")]
public class CategoriesController : ControllerBase
{
    private readonly IMediator _mediator;

    /// <summary>
    /// 初始化商品分类控制器
    /// </summary>
    public CategoriesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// 查询全部分类（下拉 / 筛选用，量小全量取，创建时间正序）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<IReadOnlyList<CategoryDto>>))]
    [HttpGet]
    public async Task<ApiResponse<IReadOnlyList<CategoryDto>>> GetCategories(CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new GetCategoriesRequest(), cancellationToken));

    /// <summary>
    /// 分页查询分类（分类管理页：名称模糊搜索 + 分页，创建时间正序）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<PagedResult<CategoryDto>>))]
    [HttpGet("paged")]
    public async Task<ApiResponse<PagedResult<CategoryDto>>> GetCategoriesPaged(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        [FromQuery] string? keyword,
        CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(
            new GetCategoriesPagedRequest { Page = page, PageSize = pageSize, Keyword = keyword },
            cancellationToken));

    /// <summary>
    /// 新增商品分类（名称唯一，大小写不敏感）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<CategoryDto>))]
    [HttpPost]
    public async Task<ApiResponse<CategoryDto>> CreateCategory([FromBody] CreateCategoryRequest request, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(request, cancellationToken));

    /// <summary>
    /// 编辑商品分类（名称唯一，排除自身；被商品引用的分类允许改名）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<CategoryDto>))]
    [HttpPut("{id:guid}")]
    public async Task<ApiResponse<CategoryDto>> UpdateCategory(
        [FromRoute] Guid id,
        [FromBody] UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        // 以路由 id 为准，避免请求体中的 id 覆盖路由
        var command = new UpdateCategoryRequest { Id = id, Name = request.Name };
        return ApiResponseFactory.Ok(await _mediator.Send(command, cancellationToken));
    }

    /// <summary>
    /// 删除商品分类（被商品引用的分类返回 40106，提示改编辑名称）
    /// </summary>
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, type: typeof(ApiResponse<object>))]
    [HttpDelete("{id:guid}")]
    public async Task<ApiResponse<object?>> DeleteCategory([FromRoute] Guid id, CancellationToken cancellationToken)
        => ApiResponseFactory.Ok(await _mediator.Send(new DeleteCategoryRequest { Id = id }, cancellationToken));
}
