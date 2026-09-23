using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Quotations.VoidQuotation;

/// <summary>
/// 报价单作废用例：不存在 → 40400；非草稿（已转订单 / 已作废）→ 40166。
/// 报价单是意向数据，作废不涉及库存、流水与收付款；作废后单号不复用。
/// </summary>
public sealed class VoidQuotationRequestHandler : IRequestHandler<VoidQuotationRequest, QuotationDetailDto>
{
    private readonly IQuotationRepository _quotationRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化报价单作废用例处理器
    /// </summary>
    public VoidQuotationRequestHandler(
        IQuotationRepository quotationRepository,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _quotationRepository = quotationRepository;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理报价单作废请求
    /// </summary>
    /// <param name="request">作废请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<QuotationDetailDto> HandleAsync(VoidQuotationRequest request, CancellationToken cancellationToken = default)
    {
        var (quotation, _) = await _quotationRepository.GetDetailAsync(request.Id, cancellationToken);
        if (quotation is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "报价单不存在");
        }

        // 仅草稿可作废；已转订单需走订单侧作废，已作废为终态（design.md §0.1）
        if (quotation.Status != QuotationStatus.Draft)
        {
            throw new BusinessException(ErrorCode.QuotationNotEditable, "报价单非草稿状态，不可作废");
        }

        var beforeStatus = quotation.Status;
        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();
        await _quotationRepository.UpdateStatusAsync(
            request.Id, QuotationStatus.Voided, null, null, operatorId, cancellationToken);

        var voidedChangeBuilder = new AuditChangeBuilder()
            .Add("status", "报价单状态", AuditText.QuotationStatus(beforeStatus), AuditText.QuotationStatus(QuotationStatus.Voided));
        await _auditLogger.RecordAsync(new AuditEntry
        {
            Resource = AuditResource.Quotation,
            Action = AuditAction.Void,
            ResourceId = quotation.Id,
            ResourceNo = quotation.QuotationNo,
            Summary = $"作废报价单 {quotation.QuotationNo}（客户：{quotation.PartnerName}、{AuditSummary.Money(quotation.TotalAmount)}）",
            Changes = voidedChangeBuilder.Build(),
            ChangesTruncated = voidedChangeBuilder.Truncated,
            UtcNow = now,
        }, cancellationToken);

        var (updated, updatedItems) = await _quotationRepository.GetDetailAsync(request.Id, cancellationToken);
        if (updated is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "报价单不存在");
        }

        return QuotationsDtoMapper.ToQuotationDetailDto(updated, updatedItems);
    }
}
