using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.Invoices.GetInvoiceById;

/// <summary>
/// 发票详情查询用例：不存在 → 40400；关联明细快照原样返回
/// </summary>
public sealed class GetInvoiceByIdRequestHandler : IRequestHandler<GetInvoiceByIdRequest, InvoiceDetailDto>
{
    private readonly IInvoiceRepository _invoiceRepository;

    /// <summary>
    /// 初始化发票详情查询用例处理器
    /// </summary>
    public GetInvoiceByIdRequestHandler(IInvoiceRepository invoiceRepository)
    {
        _invoiceRepository = invoiceRepository;
    }

    /// <summary>
    /// 处理发票详情查询请求
    /// </summary>
    /// <param name="request">详情查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<InvoiceDetailDto> HandleAsync(GetInvoiceByIdRequest request, CancellationToken cancellationToken = default)
    {
        var (invoice, items) = await _invoiceRepository.GetDetailAsync(request.Id, cancellationToken);
        if (invoice is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "发票不存在");
        }

        return InvoicesDtoMapper.ToInvoiceDetailDto(invoice, items);
    }
}