using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.Quotations.GetQuotationById;

/// <summary>
/// 报价单详情查询用例：不存在 → 40400；明细与转单信息原样返回（快照字段不联查档案）
/// </summary>
public sealed class GetQuotationByIdRequestHandler : IRequestHandler<GetQuotationByIdRequest, QuotationDetailDto>
{
    private readonly IQuotationRepository _quotationRepository;

    /// <summary>
    /// 初始化报价单详情查询用例处理器
    /// </summary>
    public GetQuotationByIdRequestHandler(IQuotationRepository quotationRepository)
    {
        _quotationRepository = quotationRepository;
    }

    /// <summary>
    /// 处理报价单详情查询请求
    /// </summary>
    /// <param name="request">详情查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<QuotationDetailDto> HandleAsync(GetQuotationByIdRequest request, CancellationToken cancellationToken = default)
    {
        var (quotation, items) = await _quotationRepository.GetDetailAsync(request.Id, cancellationToken);
        if (quotation is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "报价单不存在");
        }

        return QuotationsDtoMapper.ToQuotationDetailDto(quotation, items);
    }
}
