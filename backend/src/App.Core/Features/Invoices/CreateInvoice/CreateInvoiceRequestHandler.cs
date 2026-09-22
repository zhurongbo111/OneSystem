using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Invoices.CreateInvoice;

/// <summary>
/// 登记发票用例：发票号唯一 → 往来单位校验（存在 / 启用 / 档案类型与发票方向匹配）
/// → 逐行取被关联单据（方向匹配 / 存在 / 未作废 / 往来一致 / 开票金额不超过未开票金额）
/// → 后端重算税额与价税合计 → 同一事务：插发票 + 明细 + 操作日志（IUnitOfWork 包裹）。
/// 与收付款核销无关：不写任何单据列（已开票金额按聚合推导，见 design.md §0.3）。
/// </summary>
public sealed class CreateInvoiceRequestHandler : IRequestHandler<CreateInvoiceRequest, InvoiceDetailDto>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IInvoiceQueryRepository _invoiceQueryRepository;
    private readonly IPurchaseReceiptRepository _purchaseReceiptRepository;
    private readonly ISalesShipmentRepository _salesShipmentRepository;
    private readonly IPurchaseReturnRepository _purchaseReturnRepository;
    private readonly ISalesReturnRepository _salesReturnRepository;
    private readonly IPartnerRepository _partnerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化登记发票用例处理器
    /// </summary>
    public CreateInvoiceRequestHandler(
        IInvoiceRepository invoiceRepository,
        IInvoiceQueryRepository invoiceQueryRepository,
        IPurchaseReceiptRepository purchaseReceiptRepository,
        ISalesShipmentRepository salesShipmentRepository,
        IPurchaseReturnRepository purchaseReturnRepository,
        ISalesReturnRepository salesReturnRepository,
        IPartnerRepository partnerRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _invoiceRepository = invoiceRepository;
        _invoiceQueryRepository = invoiceQueryRepository;
        _purchaseReceiptRepository = purchaseReceiptRepository;
        _salesShipmentRepository = salesShipmentRepository;
        _purchaseReturnRepository = purchaseReturnRepository;
        _salesReturnRepository = salesReturnRepository;
        _partnerRepository = partnerRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理登记发票请求
    /// </summary>
    /// <param name="request">登记请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<InvoiceDetailDto> HandleAsync(CreateInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        // 双保险：关联明细非空（Validator 已拦格式层）
        if (request.Items is null || request.Items.Count == 0)
        {
            throw new BusinessException(ErrorCode.OrderItemsEmpty, "关联单据不能为空");
        }

        var invoiceNo = request.InvoiceNo.Trim();

        // 查库约束：发票号全局唯一
        if (await _invoiceRepository.ExistsByInvoiceNoAsync(invoiceNo, cancellationToken))
        {
            throw new BusinessException(ErrorCode.InvoiceNoExists, "发票号已存在");
        }

        // 查库约束：往来单位存在 / 启用 / 档案类型与发票方向匹配（进项为供应商、销项为客户）
        var partner = await _partnerRepository.GetByIdAsync(request.PartnerId, cancellationToken);
        if (partner is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "往来单位不存在");
        }

        if (partner.Status == PartnerStatus.Disabled)
        {
            throw new BusinessException(ErrorCode.PartnerDisabled, "往来单位已停用，不可用于开单");
        }

        if (!IsPartnerTypeMatched(request.Type, partner.Type))
        {
            throw new BusinessException(
                ErrorCode.PartnerTypeMismatch,
                request.Type == InvoiceType.Purchase ? "进项发票的往来单位必须是供应商" : "销项发票的往来单位必须是客户");
        }

        // 逐行校验被关联单据：方向 / 存在 / 未作废 / 往来一致 / 开票金额不超过未开票金额
        var lines = new List<ResolvedLine>(request.Items.Count);
        foreach (var line in request.Items)
        {
            if (!IsDirectionMatched(request.Type, line.OrderType))
            {
                throw new BusinessException(ErrorCode.InvoiceDirectionMismatch, "发票类型与被关联单据类型不匹配");
            }

            var order = await LoadOrderAsync(line.OrderType, line.OrderId, cancellationToken);
            if (!order.Exists)
            {
                throw new BusinessException(ErrorCode.NotFound, "被关联单据不存在");
            }

            if (order.Voided)
            {
                throw new BusinessException(ErrorCode.OrderVoided, "被关联单据已作废，禁止再操作");
            }

            if (order.PartnerId != partner.Id)
            {
                throw new BusinessException(ErrorCode.InvoicePartnerMismatch, $"关联单据 {order.OrderNo} 的往来单位与发票不一致");
            }

            // 未开票金额 = 单据总额 − 已开票金额（未作废发票的明细聚合，见 design.md §0.3）
            var invoicedAmount = await _invoiceQueryRepository.GetInvoicedAmountAsync(line.OrderType, line.OrderId, cancellationToken);
            var uninvoicedAmount = order.TotalAmount - invoicedAmount;
            if (line.Amount > uninvoicedAmount)
            {
                throw new BusinessException(
                    ErrorCode.InvoiceAmountExceeded,
                    $"开票金额超过单据 {order.OrderNo} 的未开票金额 {uninvoicedAmount:0.00}");
            }

            lines.Add(new ResolvedLine(line.OrderType, line.OrderId, order.OrderNo, order.OrderDate, order.TotalAmount, line.Amount));
        }

        // 后端重算税额与价税合计（忽略前端传值）
        var taxAmount = Math.Round(request.AmountExcludingTax * request.TaxRate, 2, MidpointRounding.AwayFromZero);
        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            InvoiceNo = invoiceNo,
            Type = request.Type,
            PartnerId = partner.Id,
            PartnerName = partner.Name,
            InvoiceDate = request.InvoiceDate,
            AmountExcludingTax = request.AmountExcludingTax,
            TaxRate = request.TaxRate,
            TaxAmount = taxAmount,
            TotalAmount = request.AmountExcludingTax + taxAmount,
            Status = OrderStatus.Normal,
            Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        // 明细行按请求顺序生成顺序 Guid，保证持久化顺序与请求顺序一致
        var items = lines.Select(l => new InvoiceItem
        {
            Id = SequentialGuidGenerator.NewSequential(),
            InvoiceId = invoice.Id,
            OrderType = l.OrderType,
            OrderId = l.OrderId,
            OrderNo = l.OrderNo,
            OrderDate = l.OrderDate,
            OrderTotalAmount = l.TotalAmount,
            Amount = l.Amount,
        }).ToList();

        // 业务写与审计日志必须同时成功，故由工作单元显式界定事务边界
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _invoiceRepository.AddAsync(invoice, items, cancellationToken);

            var changeBuilder = new AuditChangeBuilder()
                .Add("invoiceNo", "发票号", null, invoice.InvoiceNo)
                .Add("type", "类型", null, AuditText.InvoiceType(invoice.Type))
                .Add("partnerName", "往来单位", null, invoice.PartnerName)
                .Add("invoiceDate", "开票日期", null, AuditSummary.Date(invoice.InvoiceDate))
                .Add("amountExcludingTax", "不含税金额", null, AuditSummary.Money(invoice.AmountExcludingTax))
                .Add("taxRate", "税率", null, AuditSummary.Rate(invoice.TaxRate * 100))
                .Add("taxAmount", "税额", null, AuditSummary.Money(invoice.TaxAmount))
                .Add("totalAmount", "价税合计", null, AuditSummary.Money(invoice.TotalAmount))
                .Add("orderNos", "关联单据", null, AuditSummary.Join(items.Select(i => i.OrderNo)))
                .Add("remark", "备注", null, invoice.Remark);
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Invoice,
                Action = AuditAction.Create,
                ResourceId = invoice.Id,
                ResourceNo = invoice.InvoiceNo,
                Summary = $"登记{AuditText.InvoiceType(invoice.Type)}发票 {invoice.InvoiceNo}（{invoice.PartnerName}，金额 {AuditSummary.Money(invoice.TotalAmount)}，关联 {AuditSummary.Join(items.Select(i => i.OrderNo))}）",
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

        var (created, createdItems) = await _invoiceRepository.GetDetailAsync(invoice.Id, cancellationToken);
        if (created is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "发票登记后读取失败");
        }

        return InvoicesDtoMapper.ToInvoiceDetailDto(created, createdItems);
    }

    /// <summary>
    /// 判断发票类型与被关联单据类型是否匹配（进项 → 采购入库单 / 采购退货单；销项 → 销售出库单 / 销售退货单）
    /// </summary>
    private static bool IsDirectionMatched(InvoiceType type, SettlementOrderType orderType)
        => type == InvoiceType.Purchase
            ? orderType is SettlementOrderType.PurchaseInbound or SettlementOrderType.PurchaseReturn
            : orderType is SettlementOrderType.SalesOutbound or SettlementOrderType.SalesReturn;

    /// <summary>
    /// 判断往来单位档案类型是否满足发票方向（进项需供应商 / 两者，销项需客户 / 两者）
    /// </summary>
    private static bool IsPartnerTypeMatched(InvoiceType type, PartnerType partnerType)
        => type == InvoiceType.Purchase
            ? partnerType is PartnerType.Supplier or PartnerType.Both
            : partnerType is PartnerType.Customer or PartnerType.Both;

    /// <summary>
    /// 按被关联单据类型加载单据关键字段（核对往来 / 未开票金额 / 快照用）；
    /// 只用主表字段，故传 <c>includeItems: false</c> 不查明细分片
    /// </summary>
    private async Task<OrderInfo> LoadOrderAsync(SettlementOrderType orderType, Guid orderId, CancellationToken cancellationToken)
    {
        if (orderType == SettlementOrderType.PurchaseInbound)
        {
            var (receipt, _) = await _purchaseReceiptRepository.GetDetailAsync(orderId, includeItems: false, cancellationToken);
            return receipt is null
                ? OrderInfo.Missing
                : new OrderInfo(true, receipt.ReceiptNo, receipt.OrderDate, receipt.TotalAmount, receipt.PartnerId, receipt.Status == OrderStatus.Voided);
        }

        if (orderType == SettlementOrderType.SalesOutbound)
        {
            var (shipment, _) = await _salesShipmentRepository.GetDetailAsync(orderId, includeItems: false, cancellationToken);
            return shipment is null
                ? OrderInfo.Missing
                : new OrderInfo(true, shipment.ShipmentNo, shipment.OrderDate, shipment.TotalAmount, shipment.PartnerId, shipment.Status == OrderStatus.Voided);
        }

        if (orderType == SettlementOrderType.PurchaseReturn)
        {
            var (purchaseReturn, _) = await _purchaseReturnRepository.GetDetailAsync(orderId, includeItems: false, cancellationToken);
            return purchaseReturn is null
                ? OrderInfo.Missing
                : new OrderInfo(true, purchaseReturn.ReturnNo, purchaseReturn.ReturnDate, purchaseReturn.TotalAmount, purchaseReturn.PartnerId, purchaseReturn.Status == OrderStatus.Voided);
        }

        var (salesReturn, _) = await _salesReturnRepository.GetDetailAsync(orderId, includeItems: false, cancellationToken);
        return salesReturn is null
            ? OrderInfo.Missing
            : new OrderInfo(true, salesReturn.ReturnNo, salesReturn.ReturnDate, salesReturn.TotalAmount, salesReturn.PartnerId, salesReturn.Status == OrderStatus.Voided);
    }

    /// <summary>已校验通过的关联行（含单据快照）</summary>
    private sealed record ResolvedLine(
        SettlementOrderType OrderType,
        Guid OrderId,
        string OrderNo,
        DateTimeOffset OrderDate,
        decimal TotalAmount,
        decimal Amount);

    /// <summary>被关联单据关键字段</summary>
    private sealed record OrderInfo(
        bool Exists,
        string OrderNo,
        DateTimeOffset OrderDate,
        decimal TotalAmount,
        Guid PartnerId,
        bool Voided)
    {
        /// <summary>单据不存在</summary>
        public static OrderInfo Missing { get; } = new(false, string.Empty, default, 0m, Guid.Empty, false);
    }
}