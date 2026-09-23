using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Warehouses;
using App.Core.Finance;

namespace App.Core.Features.PurchaseReceipts.CreatePurchaseReceipt;

/// <summary>
/// 新增采购入库单用例（一步式：保存即生效）：
/// 供应商校验（存在 / 启用 / 类型含供应商）→ 商品逐行校验（存在 / 启用）
/// → 后端重算小计 / 总额（不信任前端传值）→ 单号生成（GR + yyyyMMdd + 序号，唯一索引冲突重试最多 3 次）
/// → 同一事务：插单 + 明细 + 逐行库存 += + 追加流水（IUnitOfWork 包裹）。
/// **可选关联采购订单**（specs/024-erp-order-flow design.md §3.4）：校验订单状态与未收数量，
/// 回写订单明细累计已收并推导订单流转状态，全部在同一事务内完成。
/// </summary>
public sealed class CreatePurchaseReceiptRequestHandler : IRequestHandler<CreatePurchaseReceiptRequest, PurchaseReceiptDetailDto>
{
    /// <summary>采购入库单号前缀（销售出库单为 GI，见 design.md §2.1）</summary>
    private const string ReceiptNoPrefix = "GR";

    /// <summary>单号冲突重试上限（含首次）</summary>
    private const int MaxOrderNoAttempts = 3;

    private readonly IPurchaseReceiptRepository _purchaseReceiptRepository;
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IPartnerRepository _partnerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IVoucherRepository _voucherRepository;
    private readonly IAccountMappingRepository _accountMappingRepository;
    private readonly IAccountingPeriodRepository _accountingPeriodRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化新增采购入库单用例处理器
    /// </summary>
    public CreatePurchaseReceiptRequestHandler(
        IPurchaseReceiptRepository purchaseReceiptRepository,
        IPurchaseOrderRepository purchaseOrderRepository,
        IPartnerRepository partnerRepository,
        IProductRepository productRepository,
        IWarehouseRepository warehouseRepository,
        IInventoryRepository inventoryRepository,
        IStockMovementRepository stockMovementRepository,
        IVoucherRepository voucherRepository,
        IAccountMappingRepository accountMappingRepository,
        IAccountingPeriodRepository accountingPeriodRepository,
        IAccountRepository accountRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _purchaseReceiptRepository = purchaseReceiptRepository;
        _purchaseOrderRepository = purchaseOrderRepository;
        _partnerRepository = partnerRepository;
        _productRepository = productRepository;
        _warehouseRepository = warehouseRepository;
        _inventoryRepository = inventoryRepository;
        _stockMovementRepository = stockMovementRepository;
        _voucherRepository = voucherRepository;
        _accountMappingRepository = accountMappingRepository;
        _accountingPeriodRepository = accountingPeriodRepository;
        _accountRepository = accountRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理新增采购入库单请求
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PurchaseReceiptDetailDto> HandleAsync(CreatePurchaseReceiptRequest request, CancellationToken cancellationToken = default)
    {
        // 双保险：明细非空（Validator 已拦格式层）
        if (request.Items is null || request.Items.Count == 0)
        {
            throw new BusinessException(ErrorCode.OrderItemsEmpty, "单据明细不能为空");
        }

        // 查库约束：供应商存在 / 启用 / 类型含供应商（纯客户不可开采购入库单）
        var partner = await _partnerRepository.GetByIdAsync(request.PartnerId, cancellationToken);
        if (partner is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "供应商不存在");
        }

        if (partner.Status == PartnerStatus.Disabled)
        {
            throw new BusinessException(ErrorCode.PartnerDisabled, "供应商已停用，不可用于开单");
        }

        if (partner.Type is not (PartnerType.Supplier or PartnerType.Both))
        {
            throw new BusinessException(ErrorCode.PartnerTypeMismatch, "往来单位类型与采购入库单不匹配");
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

        // 入库仓解析（038 §3.4 第 1 步）：入参可空 → 默认仓；指定仓不存在 40400、已停用 40123
        var warehouse = await WarehouseResolver.ResolveAsync(_warehouseRepository, request.WarehouseId, cancellationToken);

        // 关联订单校验（Handler 业务约束，design.md §3.4）：
        // 订单存在 → 已作废 → 已完成 / 已关闭 → 供应商一致 → 逐行明细归属与未收数量
        PurchaseOrder? linkedOrder = null;
        IReadOnlyDictionary<Guid, PurchaseOrderItem>? orderItems = null;
        if (request.OrderId is not null)
        {
            var (found, items) = await _purchaseOrderRepository.GetDetailAsync(request.OrderId.Value, cancellationToken);
            if (found is null)
            {
                throw new BusinessException(ErrorCode.NotFound, "关联的采购订单不存在");
            }

            if (found.FlowStatus == OrderFlowStatus.Voided)
            {
                throw new BusinessException(ErrorCode.OrderVoided, "关联的采购订单已作废，禁止收货");
            }

            if (found.FlowStatus is OrderFlowStatus.Completed or OrderFlowStatus.Closed)
            {
                throw new BusinessException(ErrorCode.OrderStateInvalid, "关联的采购订单当前状态不允许收货");
            }

            if (found.PartnerId != partner.Id)
            {
                throw new BusinessException(ErrorCode.OrderPartnerMismatch, "入库单的供应商与所关联订单不一致");
            }

            orderItems = items.ToDictionary(i => i.Id);
            foreach (var line in request.Items)
            {
                if (line.OrderItemId is null || !orderItems.TryGetValue(line.OrderItemId.Value, out var orderItem))
                {
                    throw new BusinessException(ErrorCode.NotFound, "关联的采购订单明细行不存在");
                }

                var remaining = orderItem.Quantity - orderItem.FulfilledQuantity;
                if (line.Quantity > remaining)
                {
                    throw new BusinessException(
                        ErrorCode.OrderFulfillExceeded,
                        $"本次入库数量超过订单未收数量：订单 {found.OrderNo}、商品 {orderItem.ProductName}、未收 {remaining}");
                }
            }

            linkedOrder = found;
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();

        // 事务：插单 + 明细 + 逐行库存 += + 流水 + 订单累计量回写与状态推导；
        // 单号冲突（唯一索引）时回滚后重新生成单号重试
        for (var attempt = 1; ; attempt++)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var receiptNo = await _purchaseReceiptRepository.GenerateOrderNoAsync(ReceiptNoPrefix, request.OrderDate, cancellationToken);
                var order = new PurchaseReceipt
                {
                    Id = Guid.NewGuid(),
                    ReceiptNo = receiptNo,
                    PartnerId = partner.Id,
                    PartnerName = partner.Name,
                    WarehouseId = warehouse.Id,
                    WarehouseName = warehouse.Name,
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

                // 明细行按请求顺序生成顺序 Guid（SequentialGuidGenerator，见 design.md §3.1），
                // 保证持久化顺序与请求顺序一致（仓储按 Id 排序还原明细顺序）
                var items = new List<PurchaseReceiptItem>(request.Items.Count);
                foreach (var line in request.Items)
                {
                    var p = products[line.ProductId];
                    items.Add(new PurchaseReceiptItem
                    {
                        Id = SequentialGuidGenerator.NewSequential(),
                        ReceiptId = order.Id,
                        ProductId = line.ProductId,
                        ProductName = p.Name,
                        Unit = p.Unit,
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice,
                        Subtotal = line.UnitPrice * line.Quantity,
                        OrderItemId = line.OrderItemId,
                    });
                }

                await _purchaseReceiptRepository.AddAsync(order, items, cancellationToken);
                foreach (var item in items)
                {
                    // 采购入库：入库仓库存 += 数量（同事务，回冲在作废用例执行）
                    await _inventoryRepository.IncrementAsync(
                        item.ProductId, warehouse.Id, item.Quantity, cancellationToken);

                    // 成本：入库按采购单明细单价加权（erp-cost design §0.2）—— 先加数量再加金额（按仓分账）
                    await _inventoryRepository.ApplyInboundCostAsync(
                        item.ProductId, warehouse.Id, item.Quantity, item.UnitPrice, cancellationToken);

                    // 库存流水：与库存增减同事务，1:1 追加并带变动仓（erp-stock-movement design §3.7）
                    await _stockMovementRepository.AppendAsync(new StockMovement
                    {
                        Id = Guid.NewGuid(),
                        ProductId = item.ProductId,
                        WarehouseId = warehouse.Id,
                        MovementType = StockMovementType.PurchaseInbound,
                        Quantity = item.Quantity,
                        UnitCost = item.UnitPrice,
                        TotalCost = CostCalculator.TotalCost(item.Quantity, item.UnitPrice),
                        SourceId = order.Id,
                        SourceNo = receiptNo,
                        CreatedAt = now,
                        CreatedBy = operatorId,
                    }, cancellationToken);
                }

                if (linkedOrder is not null && orderItems is not null)
                {
                    // 回写订单明细累计已收（原子累加），再按「本次累计后的未执行量」推导订单状态
                    foreach (var line in request.Items)
                    {
                        await _purchaseOrderRepository.AddFulfilledQuantityAsync(line.OrderItemId!.Value, line.Quantity, cancellationToken);
                    }

                    var flowStatus = DeriveFlowStatus(orderItems, request.Items);
                    await _purchaseOrderRepository.UpdateFlowStatusAsync(linkedOrder.Id, flowStatus, operatorId, cancellationToken);
                }

                // 总账（erp-general-ledger）：同事务生成自动凭证（借存货 / 贷应付账款）；
                // 科目映射缺失（40158）或期间不可记账（40154 / 40159）会阻断整单，随事务回滚
                await VoucherWriter.AppendAutoAsync(
                    VoucherSourceType.PurchaseInbound,
                    order.Id,
                    order.ReceiptNo,
                    order.OrderDate,
                    order.TotalAmount,
                    0m,
                    null,
                    _voucherRepository,
                    _accountMappingRepository,
                    _accountingPeriodRepository,
                    _accountRepository,
                    operatorId,
                    cancellationToken);

                // 业务写成功后、提交前追加操作日志：与业务同事务，异常回滚则不产生日志
                var createdReceiptChangeBuilder = new AuditChangeBuilder()
                    .Add("receiptNo", "入库单号", null, order.ReceiptNo)
                    .Add("partnerName", "供应商", null, order.PartnerName)
                    .Add("warehouseName", "入库仓", null, order.WarehouseName)
                    .Add("orderDate", "入库日期", null, AuditSummary.Date(order.OrderDate))
                    .Add("orderNo", "关联订单号", null, order.OrderNo)
                    .Add("totalAmount", "入库金额", null, AuditSummary.Money(order.TotalAmount))
                    .Add("remark", "备注", null, order.Remark);
                await _auditLogger.RecordAsync(new AuditEntry
                {
                    Resource = AuditResource.PurchaseReceipt,
                    Action = AuditAction.Create,
                    ResourceId = order.Id,
                    ResourceNo = order.ReceiptNo,
                    Summary = $"创建采购入库单 {order.ReceiptNo}（供应商：{order.PartnerName}、入库仓：{order.WarehouseName}、{AuditSummary.Count(items.Count)} 行、{AuditSummary.Money(order.TotalAmount)}）",
                    Changes = createdReceiptChangeBuilder.Build(),
                    ChangesTruncated = createdReceiptChangeBuilder.Truncated,
                    UtcNow = now,
                }, cancellationToken);

                await _unitOfWork.CommitAsync(cancellationToken);

                var (createdOrder, createdItems) = await _purchaseReceiptRepository.GetDetailAsync(order.Id, cancellationToken: cancellationToken);
                if (createdOrder is null)
                {
                    throw new BusinessException(ErrorCode.NotFound, "采购入库单创建后读取失败");
                }

                return PurchaseReceiptsDtoMapper.ToPurchaseReceiptDetailDto(createdOrder, createdItems);
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
    /// 按「本次收货后的累计已收量」推导订单流转状态（design.md §3.4）：
    /// 全部执行完 → 已完成；部分执行 → 部分收货；否则保持待收货。
    /// </summary>
    private static OrderFlowStatus DeriveFlowStatus(
        IReadOnlyDictionary<Guid, PurchaseOrderItem> orderItems,
        IReadOnlyList<CreatePurchaseReceiptItem> receivedLines)
    {
        var received = new Dictionary<Guid, int>(receivedLines.Count);
        foreach (var line in receivedLines)
        {
            var key = line.OrderItemId!.Value;
            received[key] = received.TryGetValue(key, out var accumulated) ? accumulated + line.Quantity : line.Quantity;
        }

        var allFulfilled = orderItems.Values.All(i =>
            i.FulfilledQuantity + (received.TryGetValue(i.Id, out var quantity) ? quantity : 0) >= i.Quantity);
        if (allFulfilled)
        {
            return OrderFlowStatus.Completed;
        }

        var anyFulfilled = orderItems.Values.Any(i =>
            i.FulfilledQuantity + (received.TryGetValue(i.Id, out var quantity) ? quantity : 0) > 0);
        return anyFulfilled ? OrderFlowStatus.Partial : OrderFlowStatus.Pending;
    }
}
