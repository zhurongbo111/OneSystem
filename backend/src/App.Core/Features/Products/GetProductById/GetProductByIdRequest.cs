using App.Core.Abstractions;

namespace App.Core.Features.Products.GetProductById;

/// <summary>
/// 按 id 查询商品详情请求
/// </summary>
public sealed class GetProductByIdRequest : IRequest<ProductDto>
{
    /// <summary>商品 id</summary>
    public Guid Id { get; init; }
}
