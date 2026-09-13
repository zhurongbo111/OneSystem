using App.Core.Abstractions;

namespace App.Core.Features.Products.UpdateProductStatus;

/// <summary>
/// 商品停用 / 启用请求（商品只停用不删除，保留历史单据引用）
/// </summary>
public sealed class UpdateProductStatusRequest : IRequest<ProductDto>
{
    /// <summary>商品 id</summary>
    public Guid Id { get; init; }

    /// <summary>目标状态（0 停用 / 1 启用）</summary>
    public int Status { get; init; }
}
