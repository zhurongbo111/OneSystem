using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.PartnerPrices.GetPartnerPriceById;

/// <summary>
/// 客户协议价详情查询用例：取协议价 → 联查客户名与商品 → 映射详情出参
/// </summary>
public sealed class GetPartnerPriceByIdRequestHandler : IRequestHandler<GetPartnerPriceByIdRequest, PartnerPriceDetailDto>
{
    private readonly IPartnerPriceRepository _partnerPriceRepository;
    private readonly IPartnerRepository _partnerRepository;
    private readonly IProductRepository _productRepository;

    /// <summary>
    /// 初始化客户协议价详情查询用例处理器
    /// </summary>
    public GetPartnerPriceByIdRequestHandler(
        IPartnerPriceRepository partnerPriceRepository,
        IPartnerRepository partnerRepository,
        IProductRepository productRepository)
    {
        _partnerPriceRepository = partnerPriceRepository;
        _partnerRepository = partnerRepository;
        _productRepository = productRepository;
    }

    /// <summary>
    /// 处理客户协议价详情查询请求
    /// </summary>
    /// <param name="request">查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PartnerPriceDetailDto> HandleAsync(GetPartnerPriceByIdRequest request, CancellationToken cancellationToken = default)
    {
        var partnerPrice = await _partnerPriceRepository.GetByIdAsync(request.Id, cancellationToken);
        if (partnerPrice is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "客户协议价不存在");
        }

        var partner = await _partnerRepository.GetByIdAsync(partnerPrice.PartnerId, cancellationToken);
        var product = await _productRepository.GetByIdAsync(partnerPrice.ProductId, cancellationToken);
        if (partner is null || product is null)
        {
            // 外键保证通常会存在；取不到说明数据异常，按资源不存在处理（不吞异常）
            throw new BusinessException(ErrorCode.NotFound, "客户协议价关联的客户或商品不存在");
        }

        return PartnerPricesDtoMapper.ToPartnerPriceDetailDto(partnerPrice, partner.Name, product);
    }
}