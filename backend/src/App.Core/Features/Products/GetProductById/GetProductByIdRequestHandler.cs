using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.Products.GetProductById;

/// <summary>
/// 商品详情用例：联查库存 + 分类名；不存在返回业务错误
/// </summary>
public sealed class GetProductByIdRequestHandler : IRequestHandler<GetProductByIdRequest, ProductDto>
{
    private readonly IProductRepository _productRepository;

    /// <summary>
    /// 初始化商品详情用例处理器
    /// </summary>
    public GetProductByIdRequestHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    /// <summary>
    /// 处理商品详情请求
    /// </summary>
    /// <param name="request">详情请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ProductDto> HandleAsync(GetProductByIdRequest request, CancellationToken cancellationToken = default)
    {
        var detail = await _productRepository.GetDetailAsync(request.Id, cancellationToken);
        if (detail is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "商品不存在");
        }

        return new ProductDto
        {
            Id = detail.Id.ToString(),
            Code = detail.Code,
            Name = detail.Name,
            CategoryId = detail.CategoryId.ToString(),
            CategoryName = detail.CategoryName,
            Unit = detail.Unit,
            PurchasePrice = detail.PurchasePrice,
            SalePrice = detail.SalePrice,
            SafetyStock = detail.SafetyStock,
            StockQuantity = detail.StockQuantity,
            IsBelowSafetyStock = detail.SafetyStock > 0 && detail.StockQuantity < detail.SafetyStock,
            Status = (int)detail.Status,
            Remark = detail.Remark,
            CreatedAt = detail.CreatedAt,
            UpdatedAt = detail.UpdatedAt,
        };
    }
}
