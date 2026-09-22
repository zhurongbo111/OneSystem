using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Invoices.VoidInvoice;

/// <summary>
/// 作废发票用例：不存在 → 40400；已作废 → 40104（幂等防重）。
/// 只改主表状态 + 审计，**不写任何单据列**：已开票金额按聚合推导，作废即自动释放（design.md §0.3 / §5）。
/// </summary>
public sealed class VoidInvoiceRequestHandler : IRequestHandler<VoidInvoiceRequest, InvoiceDetailDto>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化作废发票用例处理器
    /// </summary>
    public VoidInvoiceRequestHandler(
        IInvoiceRepository invoiceRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _invoiceRepository = invoiceRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理作废发票请求
    /// </summary>
    /// <param name="request">作废请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<InvoiceDetailDto> HandleAsync(VoidInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        var (invoice, _) = await _invoiceRepository.GetDetailAsync(request.Id, cancellationToken);
        if (invoice is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "发票不存在");
        }

        if (invoice.Status == OrderStatus.Voided)
        {
            // 已作废禁止再操作（防重复作废）
            throw new BusinessException(ErrorCode.OrderVoided, "单据已作废，禁止再操作");
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _invoiceRepository.UpdateStatusAsync(request.Id, OrderStatus.Voided, operatorId, cancellationToken);

            // 业务写成功后、提交前追加操作日志：与业务同事务，异常回滚则不产生日志
            var changeBuilder = new AuditChangeBuilder()
                .Add("status", "单据状态", AuditText.OrderStatus(OrderStatus.Normal), AuditText.OrderStatus(OrderStatus.Voided));
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Invoice,
                Action = AuditAction.Void,
                ResourceId = invoice.Id,
                ResourceNo = invoice.InvoiceNo,
                Summary = $"作废{AuditText.InvoiceType(invoice.Type)}发票 {invoice.InvoiceNo}（{invoice.PartnerName}、金额 {AuditSummary.Money(invoice.TotalAmount)}）",
                Changes = changeBuilder.Build(),
                ChangesTruncated = changeBuilder.Truncated,
                UtcNow = now,
            }, cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        var (updated, updatedItems) = await _invoiceRepository.GetDetailAsync(request.Id, cancellationToken);
        if (updated is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "发票不存在");
        }

        return InvoicesDtoMapper.ToInvoiceDetailDto(updated, updatedItems);
    }
}