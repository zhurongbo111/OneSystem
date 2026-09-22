using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Settlements.CreateSettlement;

/// <summary>
/// 新增收付款单用例（核销即生效）：
/// 往来单位校验（存在 / 启用；不按收付款方向限制档案类型）→ 逐行取被核销单据（存在 / 未作废 / 往来一致 / 方向匹配 / 未结金额充足）
/// → 后端重算总额（Σ 核销金额）→ 单号生成（RC / PY + yyyyMMdd + 序号，唯一索引冲突重试最多 3 次）
/// → 同一事务：插收付款单 + 明细 + 逐行累加各单据已结算金额（IUnitOfWork 包裹）。
/// </summary>
public sealed class CreateSettlementRequestHandler : IRequestHandler<CreateSettlementRequest, SettlementDetailDto>
{
    /// <summary>单号冲突重试上限（含首次）</summary>
    private const int MaxSettlementNoAttempts = 3;

    private readonly ISettlementRepository _settlementRepository;
    private readonly IPurchaseReceiptRepository _purchaseReceiptRepository;
    private readonly ISalesShipmentRepository _salesShipmentRepository;
    private readonly IPurchaseReturnRepository _purchaseReturnRepository;
    private readonly ISalesReturnRepository _salesReturnRepository;
    private readonly IPartnerRepository _partnerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化新增收付款单用例处理器
    /// </summary>
    public CreateSettlementRequestHandler(
        ISettlementRepository settlementRepository,
        IPurchaseReceiptRepository purchaseReceiptRepository,
        ISalesShipmentRepository salesShipmentRepository,
        IPurchaseReturnRepository purchaseReturnRepository,
        ISalesReturnRepository salesReturnRepository,
        IPartnerRepository partnerRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _settlementRepository = settlementRepository;
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
    /// 处理新增收付款单请求
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<SettlementDetailDto> HandleAsync(CreateSettlementRequest request, CancellationToken cancellationToken = default)
    {
        // 双保险：核销明细非空（Validator 已拦格式层）
        if (request.Items is null || request.Items.Count == 0)
        {
            throw new BusinessException(ErrorCode.OrderItemsEmpty, "核销明细不能为空");
        }

        // 查库约束：往来单位存在 / 启用
        // 不按收付款方向限制档案类型：收款可对客户（销售回款）或供应商（收回退货退款），付款同理；
        // 往来正确性由下方「核销单据往来 == 收付款单往来」保证（单据的档案类型在其开单时已校验）
        var partner = await _partnerRepository.GetByIdAsync(request.PartnerId, cancellationToken);
        if (partner is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "往来单位不存在");
        }

        if (partner.Status == PartnerStatus.Disabled)
        {
            throw new BusinessException(ErrorCode.PartnerDisabled, "往来单位已停用，不可用于开单");
        }

        // 逐行校验被核销单据：类型方向 / 存在 / 未作废 / 往来一致 / 核销金额不超过未结金额
        var lines = new List<ResolvedLine>(request.Items.Count);
        foreach (var line in request.Items)
        {
            if (!IsDirectionMatched(request.Type, line.OrderType))
            {
                throw new BusinessException(ErrorCode.SettlementDirectionMismatch, "收付款方向与被核销单据类型不匹配");
            }

            var order = await LoadOrderAsync(line.OrderType, line.OrderId, cancellationToken);
            if (!order.Exists)
            {
                throw new BusinessException(ErrorCode.NotFound, "被核销单据不存在");
            }

            if (order.Voided)
            {
                throw new BusinessException(ErrorCode.OrderVoided, "被核销单据已作废，禁止再操作");
            }

            if (order.PartnerId != partner.Id)
            {
                throw new BusinessException(ErrorCode.SettlementPartnerMismatch, $"核销单据 {order.OrderNo} 的往来单位与收付款单不一致");
            }

            var unsettledAmount = order.TotalAmount - order.SettledAmount;
            if (line.Amount > unsettledAmount)
            {
                throw new BusinessException(
                    ErrorCode.SettlementAmountExceeded,
                    $"核销金额超过单据 {order.OrderNo} 的未结金额 {unsettledAmount:0.00}");
            }

            lines.Add(new ResolvedLine(line.OrderType, line.OrderId, order.OrderNo, order.OrderDate, order.TotalAmount, line.Amount));
        }

        // 后端重算总额（不信任前端传值）
        var totalAmount = lines.Sum(l => l.Amount);
        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();

        // 事务：插收付款单 + 明细 + 逐行累加已结算金额；单号冲突（唯一索引）时回滚后重新生成单号重试
        for (var attempt = 1; ; attempt++)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var settlementNo = await _settlementRepository.GenerateSettlementNoAsync(request.Type, request.SettlementDate, cancellationToken);
                var settlement = new Settlement
                {
                    Id = Guid.NewGuid(),
                    SettlementNo = settlementNo,
                    Type = request.Type,
                    PartnerId = partner.Id,
                    PartnerName = partner.Name,
                    SettlementDate = request.SettlementDate,
                    TotalAmount = totalAmount,
                    Method = request.Method,
                    Status = OrderStatus.Normal,
                    Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim(),
                    CreatedAt = now,
                    UpdatedAt = now,
                    CreatedBy = operatorId,
                    UpdatedBy = operatorId,
                };

                // 明细行按请求顺序生成顺序 Guid，保证持久化顺序与请求顺序一致
                var items = lines.Select(l => new SettlementItem
                {
                    Id = SequentialGuidGenerator.NewSequential(),
                    SettlementId = settlement.Id,
                    OrderType = l.OrderType,
                    OrderId = l.OrderId,
                    OrderNo = l.OrderNo,
                    OrderDate = l.OrderDate,
                    OrderTotalAmount = l.TotalAmount,
                    Amount = l.Amount,
                }).ToList();

                await _settlementRepository.AddAsync(settlement, items, cancellationToken);

                foreach (var line in lines)
                {
                    // 按被核销单据类型分派到对应单据仓储，原子累加已结算金额（同一事务）
                    await AddSettledAmountAsync(line.OrderType, line.OrderId, line.Amount, operatorId, cancellationToken);
                }

                // 业务写成功后、提交前追加操作日志：与业务同事务，异常回滚则不产生日志
                var createdSettlementChangeBuilder = new AuditChangeBuilder()
                    .Add("settlementNo", "收付款单号", null, settlement.SettlementNo)
                    .Add("type", "收付方向", null, AuditText.SettlementType(settlement.Type))
                    .Add("partnerName", "往来单位", null, settlement.PartnerName)
                    .Add("settlementDate", "收付日期", null, AuditSummary.Date(settlement.SettlementDate))
                    .Add("method", "结算方式", null, AuditText.SettlementMethod(settlement.Method))
                    .Add("totalAmount", "核销金额", null, AuditSummary.Money(settlement.TotalAmount))
                    .Add("orderNos", "核销单据", null, AuditSummary.Join(items.Select(i => i.OrderNo)))
                    .Add("remark", "备注", null, settlement.Remark);
                await _auditLogger.RecordAsync(new AuditEntry
                {
                    Resource = AuditResource.Settlement,
                    Action = AuditAction.Settle,
                    ResourceId = settlement.Id,
                    ResourceNo = settlement.SettlementNo,
                    Summary = $"{AuditText.SettlementType(settlement.Type)}{settlement.SettlementNo}（往来单位：{settlement.PartnerName}、核销 {AuditSummary.Join(items.Select(i => i.OrderNo))} 共 {AuditSummary.Money(settlement.TotalAmount)}）",
                    Changes = createdSettlementChangeBuilder.Build(),
                    ChangesTruncated = createdSettlementChangeBuilder.Truncated,
                    UtcNow = now,
                }, cancellationToken);

                await _unitOfWork.CommitAsync(cancellationToken);

                var (created, createdItems) = await _settlementRepository.GetDetailAsync(settlement.Id, cancellationToken);
                if (created is null)
                {
                    throw new BusinessException(ErrorCode.NotFound, "收付款单创建后读取失败");
                }

                return SettlementsDtoMapper.ToSettlementDetailDto(created, createdItems);
            }
            catch (OrderNoConflictException) when (attempt < MaxSettlementNoAttempts)
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                continue;
            }
            catch
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }

    /// <summary>
    /// 判断收付款方向与单据类型是否匹配（收款 → 销售出库单 / 采购退货单；付款 → 采购入库单 / 销售退货单）
    /// </summary>
    private static bool IsDirectionMatched(SettlementType type, SettlementOrderType orderType)
        => type == SettlementType.Receipt
            ? orderType is SettlementOrderType.SalesOutbound or SettlementOrderType.PurchaseReturn
            : orderType is SettlementOrderType.PurchaseInbound or SettlementOrderType.SalesReturn;

    /// <summary>
    /// 按被核销单据类型分派到对应单据仓储，原子累加已结算金额（核销为正、作废回退为负）
    /// </summary>
    private Task AddSettledAmountAsync(SettlementOrderType orderType, Guid orderId, decimal delta, Guid? operatorId, CancellationToken cancellationToken)
    {
        if (orderType == SettlementOrderType.PurchaseInbound)
        {
            return _purchaseReceiptRepository.AddSettledAmountAsync(orderId, delta, operatorId, cancellationToken);
        }

        if (orderType == SettlementOrderType.SalesOutbound)
        {
            return _salesShipmentRepository.AddSettledAmountAsync(orderId, delta, operatorId, cancellationToken);
        }

        if (orderType == SettlementOrderType.PurchaseReturn)
        {
            return _purchaseReturnRepository.AddSettledAmountAsync(orderId, delta, operatorId, cancellationToken);
        }

        return _salesReturnRepository.AddSettledAmountAsync(orderId, delta, operatorId, cancellationToken);
    }

    /// <summary>
    /// 按被核销单据类型加载单据关键字段（核对往来 / 未结金额 / 快照用）；
    /// 只用主表字段，故传 <c>includeItems: false</c> 不查明细分片（见 erp-settlement design.md §3.1.1）
    /// </summary>
    private async Task<OrderInfo> LoadOrderAsync(SettlementOrderType orderType, Guid orderId, CancellationToken cancellationToken)
    {
        if (orderType == SettlementOrderType.PurchaseInbound)
        {
            var (receipt, _) = await _purchaseReceiptRepository.GetDetailAsync(orderId, includeItems: false, cancellationToken);
            return receipt is null
                ? OrderInfo.Missing
                : new OrderInfo(true, receipt.ReceiptNo, receipt.OrderDate, receipt.TotalAmount, receipt.SettledAmount, receipt.PartnerId, receipt.Status == OrderStatus.Voided);
        }

        if (orderType == SettlementOrderType.SalesOutbound)
        {
            var (shipment, _) = await _salesShipmentRepository.GetDetailAsync(orderId, includeItems: false, cancellationToken);
            return shipment is null
                ? OrderInfo.Missing
                : new OrderInfo(true, shipment.ShipmentNo, shipment.OrderDate, shipment.TotalAmount, shipment.SettledAmount, shipment.PartnerId, shipment.Status == OrderStatus.Voided);
        }

        if (orderType == SettlementOrderType.PurchaseReturn)
        {
            var (purchaseReturn, _) = await _purchaseReturnRepository.GetDetailAsync(orderId, includeItems: false, cancellationToken);
            return purchaseReturn is null
                ? OrderInfo.Missing
                : new OrderInfo(true, purchaseReturn.ReturnNo, purchaseReturn.ReturnDate, purchaseReturn.TotalAmount, purchaseReturn.SettledAmount, purchaseReturn.PartnerId, purchaseReturn.Status == OrderStatus.Voided);
        }

        var (salesReturn, _) = await _salesReturnRepository.GetDetailAsync(orderId, includeItems: false, cancellationToken);
        return salesReturn is null
            ? OrderInfo.Missing
            : new OrderInfo(true, salesReturn.ReturnNo, salesReturn.ReturnDate, salesReturn.TotalAmount, salesReturn.SettledAmount, salesReturn.PartnerId, salesReturn.Status == OrderStatus.Voided);
    }

    /// <summary>已校验通过的核销行（含单据快照）</summary>
    private sealed record ResolvedLine(
        SettlementOrderType OrderType,
        Guid OrderId,
        string OrderNo,
        DateTimeOffset OrderDate,
        decimal TotalAmount,
        decimal Amount);

    /// <summary>被核销单据关键字段</summary>
    private sealed record OrderInfo(
        bool Exists,
        string OrderNo,
        DateTimeOffset OrderDate,
        decimal TotalAmount,
        decimal SettledAmount,
        Guid PartnerId,
        bool Voided)
    {
        /// <summary>单据不存在</summary>
        public static OrderInfo Missing { get; } = new(false, string.Empty, default, 0m, 0m, Guid.Empty, false);
    }
}
