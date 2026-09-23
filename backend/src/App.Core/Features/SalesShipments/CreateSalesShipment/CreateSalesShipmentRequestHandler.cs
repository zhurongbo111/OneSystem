using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Finance;

namespace App.Core.Features.SalesShipments.CreateSalesShipment;

/// <summary>
/// 新增销售出库单用例（一步式：保存即生效，库存立即减少）：
/// 客户校验（存在 / 启用 / 类型含客户）→ 商品逐行校验（存在 / 启用）→ 后端重算小计 / 总额
/// → 同一事务：逐行 TryDecrementAsync 扣库存（任一行不足 → 40103 回滚整单）
///   → 单号生成（GI + yyyyMMdd + 序号，唯一索引冲突重试最多 3 次）
///   → 插单 + 明细 + 追加流水。
/// **可选关联销售订单**（specs/024-erp-order-flow design.md §3.4）：校验订单状态与未发数量，
/// 回写订单明细累计已发并推导订单流转状态，全部在同一事务内完成。
/// </summary>
public sealed class CreateSalesShipmentRequestHandler : IRequestHandler<CreateSalesShipmentRequest, SalesShipmentDetailDto>
{
    /// <summary>销售出库单号前缀（采购入库单为 GR，见 design.md §2.1）</summary>
    private const string ShipmentNoPrefix = "GI";

    /// <summary>单号冲突重试上限（含首次）</summary>
    private const int MaxOrderNoAttempts = 3;

    private readonly ISalesShipmentRepository _salesShipmentRepository;
    private readonly ISalesOrderRepository _salesOrderRepository;
    private readonly IPartnerRepository _partnerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly ISettlementQueryRepository _settlementQueryRepository;
    private readonly IVoucherRepository _voucherRepository;
    private readonly IAccountMappingRepository _accountMappingRepository;
    private readonly IAccountingPeriodRepository _accountingPeriodRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化新增销售出库单用例处理器
    /// </summary>
    public CreateSalesShipmentRequestHandler(
        ISalesShipmentRepository salesShipmentRepository,
        ISalesOrderRepository salesOrderRepository,
        IPartnerRepository partnerRepository,
        IProductRepository productRepository,
        IInventoryRepository inventoryRepository,
        IStockMovementRepository stockMovementRepository,
        ISettlementQueryRepository settlementQueryRepository,
        IVoucherRepository voucherRepository,
        IAccountMappingRepository accountMappingRepository,
        IAccountingPeriodRepository accountingPeriodRepository,
        IAccountRepository accountRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _salesShipmentRepository = salesShipmentRepository;
        _salesOrderRepository = salesOrderRepository;
        _partnerRepository = partnerRepository;
        _productRepository = productRepository;
        _inventoryRepository = inventoryRepository;
        _stockMovementRepository = stockMovementRepository;
        _settlementQueryRepository = settlementQueryRepository;
        _voucherRepository = voucherRepository;
        _accountMappingRepository = accountMappingRepository;
        _accountingPeriodRepository = accountingPeriodRepository;
        _accountRepository = accountRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理新增销售出库单请求
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<SalesShipmentDetailDto> HandleAsync(CreateSalesShipmentRequest request, CancellationToken cancellationToken = default)
    {
        // 双保险：明细非空（Validator 已拦格式层）
        if (request.Items is null || request.Items.Count == 0)
        {
            throw new BusinessException(ErrorCode.OrderItemsEmpty, "单据明细不能为空");
        }

        // 查库约束：客户存在 / 启用 / 类型含客户（纯供应商不可开销售出库单）
        var partner = await _partnerRepository.GetByIdAsync(request.PartnerId, cancellationToken);
        if (partner is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "客户不存在");
        }

        if (partner.Status == PartnerStatus.Disabled)
        {
            throw new BusinessException(ErrorCode.PartnerDisabled, "客户已停用，不可用于开单");
        }

        if (partner.Type is not (PartnerType.Customer or PartnerType.Both))
        {
            throw new BusinessException(ErrorCode.PartnerTypeMismatch, "往来单位类型与销售出库单不匹配");
        }

        // 查库约束：商品逐行存在 / 启用；同时收集名称 / 单位快照，后端重算小计 / 总额（不信任前端传值）
        var products = new Dictionary<Guid, Product>(request.Items.Count);
        var totalAmount = 0m;
        foreach (var line in request.Items)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
            {
                product = await _productRepository.GetByIdAsync(line.ProductId, cancellationToken);
                if (product is null)
                {
                    throw new BusinessException(ErrorCode.NotFound, "商品不存在");
                }

                if (product.Status == ProductStatus.Disabled)
                {
                    throw new BusinessException(ErrorCode.ProductDisabled, "商品已停用，不可用于开单");
                }

                products[line.ProductId] = product;
            }

            totalAmount += line.Quantity * line.UnitPrice;
        }

        // 信用额度校验（specs/036-erp-partner-price/design.md §0.4）：额度 0 视为不限；
        // 校验位置在事务与库存扣减之前，超限即整单拒绝（不产生库存 / 单据 / 流水变更）
        if (partner.CreditLimit > 0)
        {
            var receivableAmount = await _settlementQueryRepository.GetReceivableAmountAsync(partner.Id, cancellationToken);
            if (receivableAmount + totalAmount > partner.CreditLimit)
            {
                throw new BusinessException(
                    ErrorCode.CreditLimitExceeded,
                    $"客户 {partner.Name} 超出信用额度（额度 {AuditSummary.Money(partner.CreditLimit)}，当前应收 {AuditSummary.Money(receivableAmount)}，本单 {AuditSummary.Money(totalAmount)}）");
            }
        }

        // 关联订单校验（Handler 业务约束，design.md §3.4）：订单存在 → 已作废 → 已完成 / 已关闭 → 客户一致 → 未发数量
        SalesOrder? linkedOrder = null;
        IReadOnlyDictionary<Guid, SalesOrderItem>? orderItems = null;
        if (request.OrderId is not null)
        {
            var (found, items) = await _salesOrderRepository.GetDetailAsync(request.OrderId.Value, cancellationToken);
            if (found is null)
            {
                throw new BusinessException(ErrorCode.NotFound, "关联的销售订单不存在");
            }

            if (found.FlowStatus == OrderFlowStatus.Voided)
            {
                throw new BusinessException(ErrorCode.OrderVoided, "关联的销售订单已作废，禁止发货");
            }

            if (found.FlowStatus is OrderFlowStatus.Completed or OrderFlowStatus.Closed)
            {
                throw new BusinessException(ErrorCode.OrderStateInvalid, "关联的销售订单当前状态不允许发货");
            }

            if (found.PartnerId != partner.Id)
            {
                throw new BusinessException(ErrorCode.OrderPartnerMismatch, "出库单的客户与所关联订单不一致");
            }

            orderItems = items.ToDictionary(i => i.Id);
            foreach (var line in request.Items)
            {
                if (line.OrderItemId is null || !orderItems.TryGetValue(line.OrderItemId.Value, out var orderItem))
                {
                    throw new BusinessException(ErrorCode.NotFound, "关联的销售订单明细行不存在");
                }

                var remaining = orderItem.Quantity - orderItem.FulfilledQuantity;
                if (line.Quantity > remaining)
                {
                    throw new BusinessException(
                        ErrorCode.OrderFulfillExceeded,
                        $"本次出库数量超过订单未发数量：订单 {found.OrderNo}、商品 {orderItem.ProductName}、未发 {remaining}");
                }
            }

            linkedOrder = found;
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();

        // 事务：先扣库存（防超卖），成功后再插单 + 明细 + 流水 + 订单回写；
        // 单号冲突（唯一索引）时回滚后重新生成单号重试。
        // 注意：重试意味着重新扣减——上一轮已扣库存会随回滚恢复，故重新生成单号后整轮重来是安全的。
        for (var attempt = 1; ; attempt++)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                // 成本：出库按「变动前」移动加权均价结转（erp-cost design §0.2）—— 必须在扣减前读取
                var outboundUnitCosts = new Dictionary<Guid, decimal>(request.Items.Count);
                foreach (var line in request.Items)
                {
                    outboundUnitCosts[line.ProductId] =
                        await _inventoryRepository.GetAverageCostAsync(line.ProductId, cancellationToken);
                }

                // 逐行扣减：任一行库存不足 → 回滚整单（报首个不足商品）
                foreach (var line in request.Items)
                {
                    var ok = await _inventoryRepository.TryDecrementAsync(line.ProductId, line.Quantity, cancellationToken);
                    if (!ok)
                    {
                        var current = await _inventoryRepository.GetQuantityAsync(line.ProductId, cancellationToken);
                        throw new BusinessException(
                            ErrorCode.InsufficientStock,
                            $"库存不足：商品 {products[line.ProductId].Name}（当前 {current}，需要 {line.Quantity}）");
                    }
                }

                var shipmentNo = await _salesShipmentRepository.GenerateOrderNoAsync(ShipmentNoPrefix, request.OrderDate, cancellationToken);
                var order = new SalesShipment
                {
                    Id = Guid.NewGuid(),
                    ShipmentNo = shipmentNo,
                    PartnerId = partner.Id,
                    PartnerName = partner.Name,
                    OrderDate = request.OrderDate,
                    OrderId = linkedOrder?.Id,
                    OrderNo = linkedOrder?.OrderNo,
                    TotalAmount = totalAmount,
                    SettledAmount = 0m,
                    Status = OrderStatus.Normal,
                    Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim(),
                    CreatedAt = now,
                    UpdatedAt = now,
                    CreatedBy = operatorId,
                    UpdatedBy = operatorId,
                };

                // 明细行按请求顺序生成顺序 Guid（SequentialGuidGenerator，见 design.md §3.1）
                var items = new List<SalesShipmentItem>(request.Items.Count);
                foreach (var line in request.Items)
                {
                    var p = products[line.ProductId];
                    items.Add(new SalesShipmentItem
                    {
                        Id = SequentialGuidGenerator.NewSequential(),
                        ShipmentId = order.Id,
                        ProductId = line.ProductId,
                        ProductName = p.Name,
                        Unit = p.Unit,
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice,
                        Subtotal = line.UnitPrice * line.Quantity,
                        OrderItemId = line.OrderItemId,
                    });
                }

                await _salesShipmentRepository.AddAsync(order, items, cancellationToken);

                // 库存流水：销售出库，与库存扣减同事务（逐行 TryDecrementAsync 已全部成功后才走到这里）
                // 成本结转金额 = Σ 本次出库流水成本（作为自动凭证的成本结转分录金额）
                var costAmount = 0m;
                foreach (var item in items)
                {
                    // 成本：按变动前均价结转（均价不变 —— 按均价出库不改变均值）
                    var unitCost = outboundUnitCosts[item.ProductId];
                    var totalCost = CostCalculator.TotalCost(item.Quantity, unitCost);
                    costAmount += totalCost;
                    await _inventoryRepository.ApplyOutboundCostAsync(item.ProductId, totalCost, cancellationToken);

                    await _stockMovementRepository.AppendAsync(new StockMovement
                    {
                        Id = Guid.NewGuid(),
                        ProductId = item.ProductId,
                        MovementType = StockMovementType.SalesOutbound,
                        Quantity = -item.Quantity,
                        UnitCost = unitCost,
                        TotalCost = -totalCost,
                        SourceId = order.Id,
                        SourceNo = shipmentNo,
                        CreatedAt = now,
                        CreatedBy = operatorId,
                    }, cancellationToken);
                }

                if (linkedOrder is not null && orderItems is not null)
                {
                    // 回写订单明细累计已发（原子累加），再按「本次累计后的未执行量」推导订单状态
                    foreach (var line in request.Items)
                    {
                        await _salesOrderRepository.AddFulfilledQuantityAsync(line.OrderItemId!.Value, line.Quantity, cancellationToken);
                    }

                    var flowStatus = DeriveFlowStatus(orderItems, request.Items);
                    await _salesOrderRepository.UpdateFlowStatusAsync(linkedOrder.Id, flowStatus, operatorId, cancellationToken);
                }

                // 总账（erp-general-ledger）：同事务生成自动凭证（借应收账款 / 贷主营业务收入，
                // 并附成本结转分录：借主营业务成本 / 贷库存商品）；
                // 科目映射缺失（40158）或期间不可记账（40154 / 40159）会阻断整单，随事务回滚
                await VoucherWriter.AppendAutoAsync(
                    VoucherSourceType.SalesOutbound,
                    order.Id,
                    order.ShipmentNo,
                    order.OrderDate,
                    order.TotalAmount,
                    costAmount,
                    null,
                    _voucherRepository,
                    _accountMappingRepository,
                    _accountingPeriodRepository,
                    _accountRepository,
                    operatorId,
                    cancellationToken);

                // 业务写成功后、提交前追加操作日志：与业务同事务，异常回滚则不产生日志
                var createdShipmentChangeBuilder = new AuditChangeBuilder()
                    .Add("shipmentNo", "出库单号", null, order.ShipmentNo)
                    .Add("partnerName", "客户", null, order.PartnerName)
                    .Add("orderDate", "出库日期", null, AuditSummary.Date(order.OrderDate))
                    .Add("orderNo", "关联订单号", null, order.OrderNo)
                    .Add("totalAmount", "出库金额", null, AuditSummary.Money(order.TotalAmount))
                    .Add("remark", "备注", null, order.Remark);
                await _auditLogger.RecordAsync(new AuditEntry
                {
                    Resource = AuditResource.SalesShipment,
                    Action = AuditAction.Create,
                    ResourceId = order.Id,
                    ResourceNo = order.ShipmentNo,
                    Summary = $"创建销售出库单 {order.ShipmentNo}（客户：{order.PartnerName}、{AuditSummary.Count(items.Count)} 行、{AuditSummary.Money(order.TotalAmount)}）",
                    Changes = createdShipmentChangeBuilder.Build(),
                    ChangesTruncated = createdShipmentChangeBuilder.Truncated,
                    UtcNow = now,
                }, cancellationToken);

                await _unitOfWork.CommitAsync(cancellationToken);

                var (createdOrder, createdItems) = await _salesShipmentRepository.GetDetailAsync(order.Id, cancellationToken: cancellationToken);
                if (createdOrder is null)
                {
                    throw new BusinessException(ErrorCode.NotFound, "销售出库单创建后读取失败");
                }

                return SalesShipmentsDtoMapper.ToSalesShipmentDetailDto(createdOrder, createdItems);
            }
            catch (OrderNoConflictException) when (attempt < MaxOrderNoAttempts)
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
    /// 按「本次发货后的累计已发量」推导订单流转状态（design.md §3.4）：
    /// 全部执行完 → 已完成；部分执行 → 部分发货；否则保持待发货。
    /// </summary>
    private static OrderFlowStatus DeriveFlowStatus(
        IReadOnlyDictionary<Guid, SalesOrderItem> orderItems,
        IReadOnlyList<CreateSalesShipmentItem> shippedLines)
    {
        var shipped = new Dictionary<Guid, int>(shippedLines.Count);
        foreach (var line in shippedLines)
        {
            var key = line.OrderItemId!.Value;
            shipped[key] = shipped.TryGetValue(key, out var accumulated) ? accumulated + line.Quantity : line.Quantity;
        }

        var allFulfilled = orderItems.Values.All(i =>
            i.FulfilledQuantity + (shipped.TryGetValue(i.Id, out var quantity) ? quantity : 0) >= i.Quantity);
        if (allFulfilled)
        {
            return OrderFlowStatus.Completed;
        }

        var anyFulfilled = orderItems.Values.Any(i =>
            i.FulfilledQuantity + (shipped.TryGetValue(i.Id, out var quantity) ? quantity : 0) > 0);
        return anyFulfilled ? OrderFlowStatus.Partial : OrderFlowStatus.Pending;
    }
}
