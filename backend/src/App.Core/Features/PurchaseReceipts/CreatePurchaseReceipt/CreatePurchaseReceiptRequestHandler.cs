using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Approvals;
using App.Core.Features.Batches;
using App.Core.Features.Warehouses;

namespace App.Core.Features.PurchaseReceipts.CreatePurchaseReceipt;

/// <summary>
/// 新增采购入库单用例（一步式）。
/// **未命中审批规则（042 §0.2）**：保存即生效 —— 供应商校验（存在 / 启用 / 类型含供应商）→ 商品逐行校验
///   → 后端重算小计 / 总额（不信任前端传值）→ 单号生成（GR + yyyyMMdd + 序号，唯一索引冲突重试最多 3 次）
///   → 同一事务：插单 + 明细 + <see cref="PurchaseReceiptFulfillment"/> 生效（库存 + 流水 + 成本 + 订单回写 + 凭证）。
/// **命中审批规则**：单据落库为「待审批」并生成审批记录 + 给审批人发站内信，**不产生任何库存 / 流水 / 成本变化**；
///   生效动作延迟到审批通过时由同一 <see cref="PurchaseReceiptFulfillment"/> 执行（specs/042-erp-approval）。
/// **可选关联采购订单**（specs/024-erp-order-flow design.md §3.4）：校验订单状态与未收数量保持不变。
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
    private readonly IBatchRepository _batchRepository;
    private readonly IApprovalRuleRepository _approvalRuleRepository;
    private readonly IApprovalRepository _approvalRepository;
    private readonly PurchaseReceiptFulfillment _fulfillment;
    private readonly ApprovalNotifier _approvalNotifier;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;
    private readonly ISystemClock _clock;

    /// <summary>
    /// 初始化新增采购入库单用例处理器
    /// </summary>
    public CreatePurchaseReceiptRequestHandler(
        IPurchaseReceiptRepository purchaseReceiptRepository,
        IPurchaseOrderRepository purchaseOrderRepository,
        IPartnerRepository partnerRepository,
        IProductRepository productRepository,
        IWarehouseRepository warehouseRepository,
        IBatchRepository batchRepository,
        IApprovalRuleRepository approvalRuleRepository,
        IApprovalRepository approvalRepository,
        PurchaseReceiptFulfillment fulfillment,
        ApprovalNotifier approvalNotifier,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger,
        ISystemClock clock)
    {
        _purchaseReceiptRepository = purchaseReceiptRepository;
        _purchaseOrderRepository = purchaseOrderRepository;
        _partnerRepository = partnerRepository;
        _productRepository = productRepository;
        _warehouseRepository = warehouseRepository;
        _batchRepository = batchRepository;
        _approvalRuleRepository = approvalRuleRepository;
        _approvalRepository = approvalRepository;
        _fulfillment = fulfillment;
        _approvalNotifier = approvalNotifier;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
        _clock = clock;
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

            var orderItems = items.ToDictionary(i => i.Id);
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

        // 审批规则判定（042 §0.1）：规则启用 且 金额 ≥ 阈值 → 触发审批（保存时不生效）
        var rule = await _approvalRuleRepository.GetAsync(SettlementOrderType.PurchaseInbound, cancellationToken);
        var requiresApproval = rule is not null && rule.Enabled && totalAmount >= rule.ThresholdAmount;

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId()
            ?? throw new BusinessException(ErrorCode.Unauthorized, "登录状态无效，请重新登录");
        Approval? pendingApproval = null;

        // 事务：插单 + 明细 +（未命中时）生效动作 /（命中时）审批记录；
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
                    ApprovalStatus = requiresApproval ? ApprovalStatus.Pending : ApprovalStatus.None,
                    Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim(),
                    CreatedAt = now,
                    UpdatedAt = now,
                    CreatedBy = operatorId,
                    UpdatedBy = operatorId,
                };

                // 批次解析（040，事务内执行：就地新建批次与单据同事务，回滚时批次一并回滚）
                // 入库类不拦截过期批次（outbound = false）
                var resolvedBatches = new List<ResolvedBatch?>(request.Items.Count);
                foreach (var line in request.Items)
                {
                    var p0 = products[line.ProductId];
                    resolvedBatches.Add(await BatchLineResolver.ResolveAsync(
                        p0.IsBatchManaged,
                        p0.Id,
                        line.BatchId,
                        line.NewBatchNo,
                        line.NewProductionDate,
                        line.NewExpiryDate,
                        _batchRepository,
                        outbound: false,
                        _clock.Today,
                        operatorId,
                        cancellationToken));
                }

                // 明细行按请求顺序生成顺序 Guid（SequentialGuidGenerator，见 design.md §3.1），
                // 保证持久化顺序与请求顺序一致（仓储按 Id 排序还原明细顺序）
                var items = new List<PurchaseReceiptItem>(request.Items.Count);
                for (var index = 0; index < request.Items.Count; index++)
                {
                    var line = request.Items[index];
                    var p = products[line.ProductId];
                    var batch = resolvedBatches[index];
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
                        BatchId = batch?.BatchId,
                        BatchNo = batch?.BatchNo,
                    });
                }

                await _purchaseReceiptRepository.AddAsync(order, items, cancellationToken);

                if (requiresApproval)
                {
                    // 命中审批：仅落单 + 审批记录，**不产生任何库存 / 流水 / 成本变化**（042 §0.2）
                    pendingApproval = new Approval
                    {
                        Id = Guid.NewGuid(),
                        OrderType = SettlementOrderType.PurchaseInbound,
                        OrderId = order.Id,
                        OrderNo = order.ReceiptNo,
                        PartnerName = order.PartnerName,
                        Amount = order.TotalAmount,
                        Status = ApprovalStatus.Pending,
                        SubmittedBy = operatorId,
                        SubmittedAt = now,
                    };
                    await _approvalRepository.AddAsync(pendingApproval, cancellationToken);
                }
                else
                {
                    // 未命中审批：保存即生效（与改造前逐条一致，由共享生效组件执行）
                    await _fulfillment.ApplyAsync(order, items, operatorId, now, cancellationToken);
                }

                // 业务写成功后、提交前追加操作日志：与业务同事务，异常回滚则不产生日志
                var createdReceiptChangeBuilder = new AuditChangeBuilder()
                    .Add("receiptNo", "入库单号", null, order.ReceiptNo)
                    .Add("partnerName", "供应商", null, order.PartnerName)
                    .Add("warehouseName", "入库仓", null, order.WarehouseName)
                    .Add("orderDate", "入库日期", null, AuditSummary.Date(order.OrderDate))
                    .Add("orderNo", "关联订单号", null, order.OrderNo)
                    .Add("totalAmount", "入库金额", null, AuditSummary.Money(order.TotalAmount))
                    .Add("remark", "备注", null, order.Remark);
                var createdSummary = $"创建采购入库单 {order.ReceiptNo}（供应商：{order.PartnerName}、入库仓：{order.WarehouseName}、{AuditSummary.Count(items.Count)} 行、{AuditSummary.Money(order.TotalAmount)}）{AuditSummary.BatchItems(items, i => i.ProductName, i => i.BatchNo)}";
                await _auditLogger.RecordAsync(new AuditEntry
                {
                    Resource = AuditResource.PurchaseReceipt,
                    Action = AuditAction.Create,
                    ResourceId = order.Id,
                    ResourceNo = order.ReceiptNo,
                    Summary = requiresApproval ? $"{createdSummary}｜已提交审批（审批通过后才产生库存变动）" : createdSummary,
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

                // 站内信在提交之后发送：发信失败只记日志，不影响已落库单据（042 §3.5）
                if (pendingApproval is not null)
                {
                    await _approvalNotifier.NotifyPendingAsync(pendingApproval, cancellationToken);
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
}
