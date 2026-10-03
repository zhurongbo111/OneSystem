using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.Batches.GetBatchById;

/// <summary>
/// 查询批次详情用例：按 id 取批次（不存在 → 40400，含已停用），联查商品编码 / 名称
/// </summary>
public sealed class GetBatchByIdRequestHandler : IRequestHandler<GetBatchByIdRequest, BatchDetailDto>
{
    private readonly IBatchRepository _batchRepository;
    private readonly IProductRepository _productRepository;

    /// <summary>
    /// 初始化查询批次详情用例处理器
    /// </summary>
    public GetBatchByIdRequestHandler(IBatchRepository batchRepository, IProductRepository productRepository)
    {
        _batchRepository = batchRepository;
        _productRepository = productRepository;
    }

    /// <summary>
    /// 处理查询批次详情请求
    /// </summary>
    /// <param name="request">查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<BatchDetailDto> HandleAsync(GetBatchByIdRequest request, CancellationToken cancellationToken = default)
    {
        var batch = await _batchRepository.GetByIdAsync(request.Id, cancellationToken);
        if (batch is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "批次不存在");
        }

        var product = await _productRepository.GetByIdAsync(batch.ProductId, cancellationToken);

        return BatchDtoMapper.ToDetailDto(batch, product?.Code, product?.Name);
    }
}
