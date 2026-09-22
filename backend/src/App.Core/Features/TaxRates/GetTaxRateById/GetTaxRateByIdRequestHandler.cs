using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.TaxRates.GetTaxRateById;

/// <summary>
/// 税率详情用例：按 id 查询，不存在返回 40400
/// </summary>
public sealed class GetTaxRateByIdRequestHandler : IRequestHandler<GetTaxRateByIdRequest, TaxRateDetailDto>
{
    private readonly ITaxRateRepository _taxRateRepository;

    /// <summary>
    /// 初始化税率详情用例处理器
    /// </summary>
    public GetTaxRateByIdRequestHandler(ITaxRateRepository taxRateRepository)
    {
        _taxRateRepository = taxRateRepository;
    }

    /// <summary>
    /// 处理税率详情请求
    /// </summary>
    /// <param name="request">详情请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<TaxRateDetailDto> HandleAsync(GetTaxRateByIdRequest request, CancellationToken cancellationToken = default)
    {
        var taxRate = await _taxRateRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new BusinessException(ErrorCode.NotFound, "税率不存在");

        return TaxRateDtoMapper.ToTaxRateDetailDto(taxRate);
    }
}