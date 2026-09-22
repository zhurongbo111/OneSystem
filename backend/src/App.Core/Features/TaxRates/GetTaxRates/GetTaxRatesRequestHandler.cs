using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Responses;

namespace App.Core.Features.TaxRates.GetTaxRates;

/// <summary>
/// 税率分页列表用例：按关键词 / 状态筛选后分页查询，直接依赖仓储，无 Service 层
/// </summary>
public sealed class GetTaxRatesRequestHandler : IRequestHandler<GetTaxRatesRequest, PagedResult<TaxRateListItemDto>>
{
    private readonly ITaxRateRepository _taxRateRepository;

    /// <summary>
    /// 初始化税率分页列表用例处理器
    /// </summary>
    public GetTaxRatesRequestHandler(ITaxRateRepository taxRateRepository)
    {
        _taxRateRepository = taxRateRepository;
    }

    /// <summary>
    /// 处理税率分页列表请求
    /// </summary>
    /// <param name="request">列表请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<TaxRateListItemDto>> HandleAsync(GetTaxRatesRequest request, CancellationToken cancellationToken = default)
    {
        var status = request.Status is null ? (TaxRateStatus?)null : (TaxRateStatus)request.Status.Value;
        var (items, total) = await _taxRateRepository.GetPagedAsync(
            request.Keyword,
            status,
            request.Page,
            request.PageSize,
            cancellationToken);

        return new PagedResult<TaxRateListItemDto>
        {
            Items = items.Select(TaxRateDtoMapper.ToTaxRateListItemDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}