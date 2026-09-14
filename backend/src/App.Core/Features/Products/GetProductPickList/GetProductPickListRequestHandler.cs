using App.Core.Abstractions;

namespace App.Core.Features.Products.GetProductPickList;

/// <summary>
/// 开单商品选择用例：仅启用商品（仓储已过滤），全量返回，供 erp-purchase / erp-sale 开单页消费
/// </summary>
public sealed class GetProductPickListRequestHandler : IRequestHandler<GetProductPickListRequest, IReadOnlyList<ProductPickDto>>
{
    private readonly IProductRepository _productRepository;

    /// <summary>
    /// 初始化开单商品选择用例处理器
    /// </summary>
    public GetProductPickListRequestHandler(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    /// <summary>
    /// 处理开单商品选择请求
    /// </summary>
    /// <param name="request">选择请求（空参数）</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<IReadOnlyList<ProductPickDto>> HandleAsync(GetProductPickListRequest request, CancellationToken cancellationToken = default)
    {
        var items = await _productRepository.GetPickListAsync(cancellationToken);
        return items.Select(ProductDtoMapper.ToProductPickDto).ToList();
    }
}
