using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Approvals;
using App.Core.Features.Batches;
using App.Core.Features.Warehouses;

namespace App.Core.Features.SalesShipments.CreateSalesShipment;

/// <summary>
/// 新增销售出库单用例（一步式）。
/// **未命中审批规则（042 §0.2）**：保存即生效 —— 客户校验（存在 / 启用 / 类型含客户）→ 商品逐行校验
///   → 后端重算小计 / 总额 → 信用额度校验 → 单号生成（GI + yyyyMMdd + 序号，唯一索引冲突重试最多 3 次）
///   → 同一事务：<see cref="SalesShipmentFulfillment"/> 生效（条件扣减库存，任一行不足 → 40103 回滚整单 + 流水 + 凭证）。
/// **命中审批规则**：单据落库为「待审批」并生成审批记录 + 给审批人发站内信，**不产生任何库存 / 流水 / 成本变化**；
///   生效动作延迟到审批通过时由同一 <see cref="SalesShipmentFulfillment"/> 执行（specs/042-erp-approval）。
/// **可选关联销售订单**（specs/024-erp-order-flow design.md §3.4）：校验订单状态与未发数量保持不变。
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
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly ISettlementQueryRepository _settlementQueryRepository;
    private readonly IBatchRepository _batchRepository;
    private readonly IApprovalRuleRepository _approvalRuleRepository;
    private readonly IApprovalRepository _approvalRepository;
    private readonly SalesShipmentFulfillment _fulfillment;
    private readonly ApprovalNotifier _approvalNotifier;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;
    private readonly ISystemClock _clock;

    /// <summary>
    /// 初始化新增销售出库单用例处理器
    /// </summary>
    public CreateSalesShipmentRequestHandler(
        ISalesShipmentRepository salesShipmentRepository,
        ISalesOrderRepository salesOrderRepository,
        IPartnerRepository partnerRepository,
        IProductRepository productRepository,
        IWarehouseRepository warehouseRepository,
        ISettlementQueryRepository settlementQueryRepository,
        IBatchRepository batchRepository,
        IApprovalRuleRepository approvalRuleRepository,
        IApprovalRepository approvalRepository,
        SalesShipmentFulfillment fulfillment,
        ApprovalNotifier approvalNotifier,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger,
        ISystemClock clock)
    {
        _salesShipmentRepository = salesShipmentRepository;
        _salesOrderRepository = salesOrderRepository;
        _partnerRepository = partnerRepository;
        _productRepository = productRepository;
        _warehouseRepository = warehouseRepository;
        _settlementQueryRepository = settlementQueryRepository;
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

        // 出库仓解析（038 §3.4 第 1 步）：入参可空 → 默认仓；指定仓不存在 40400、已停用 40123
        var warehouse = await WarehouseResolver.ResolveAsync(_warehouseRepository, request.WarehouseId, cancellationToken);

        // 关联订单校验（Handler 业务约束，design.md §3.4）：订单存在 → 已作废 → 已完成 / 已关闭 → 客户一致 → 未发数量
        SalesOrder? linkedOrder = null;
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

            var orderItems = items.ToDictionary(i => i.Id);
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

        // 审批规则判定（042 §0.1）：规则启用 且 金额 ≥ 阈值 → 触发审批（保存时不生效）
        var rule = await _approvalRuleRepository.GetAsync(SettlementOrderType.SalesOutbound, cancellationToken);
        var requiresApproval = rule is not null && rule.Enabled && totalAmount >= rule.ThresholdAmount;

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId()
            ?? throw new BusinessException(ErrorCode.Unauthorized, "登录状态无效，请重新登录");
        Approval? pendingApproval = null;

        // 事务：插单 + 明细 +（未命中时）生效动作 /（命中时）审批记录；
        // 单号冲突（唯一索引）时回滚后重新生成单号重试。
        // 注意：重试意味着重新走整轮（上一轮库存扣减随回滚恢复，故重新生成单号后整轮重来是安全的）。
        for (var attempt = 1; ; attempt++)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                // 批次解析（040，事务内执行）：出库类拦截过期批次（outbound = true → 40128）；
                // 按批次商品缺批次 → 40127；停用批次 → 40000
                var resolvedBatches = new List<ResolvedBatch?>(request.Items.Count);
                foreach (var line in request.Items)
                {
                    var p0 = products[line.ProductId];
                    resolvedBatches.Add(await BatchLineResolver.ResolveAsync(
                        p0.IsBatchManaged,
                        p0.Id,
                        line.BatchId,
                        null,
                        null,
                        null,
                        _batchRepository,
                        outbound: true,
                        _clock.Today,
                        operatorId,
                        cancellationToken));
                }

                var shipmentNo = await _salesShipmentRepository.GenerateOrderNoAsync(ShipmentNoPrefix, request.OrderDate, cancellationToken);
                var order = new SalesShipment
                {
                    Id = Guid.NewGuid(),
                    ShipmentNo = shipmentNo,
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

                // 明细行按请求顺序生成顺序 Guid（SequentialGuidGenerator，见 design.md §3.1）
                var items = new List<SalesShipmentItem>(request.Items.Count);
                for (var index = 0; index < request.Items.Count; index++)
                {
                    var line = request.Items[index];
                    var p = products[line.ProductId];
                    var batch = resolvedBatches[index];
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
                        BatchId = batch?.BatchId,
                        BatchNo = batch?.BatchNo,
                    });
                }

                await _salesShipmentRepository.AddAsync(order, items, cancellationToken);

                if (requiresApproval)
                {
                    // 命中审批：仅落单 + 审批记录，**不产生任何库存 / 流水 / 成本变化**（042 §0.2）
                    pendingApproval = new Approval
                    {
                        Id = Guid.NewGuid(),
                        OrderType = SettlementOrderType.SalesOutbound,
                        OrderId = order.Id,
                        OrderNo = order.ShipmentNo,
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
                var createdShipmentChangeBuilder = new AuditChangeBuilder()
                    .Add("shipmentNo", "出库单号", null, order.ShipmentNo)
                    .Add("partnerName", "客户", null, order.PartnerName)
                    .Add("warehouseName", "出库仓", null, order.WarehouseName)
                    .Add("orderDate", "出库日期", null, AuditSummary.Date(order.OrderDate))
                    .Add("orderNo", "关联订单号", null, order.OrderNo)
                    .Add("totalAmount", "出库金额", null, AuditSummary.Money(order.TotalAmount))
                    .Add("remark", "备注", null, order.Remark);
                var createdSummary = $"创建销售出库单 {order.ShipmentNo}（客户：{order.PartnerName}、出库仓：{order.WarehouseName}、{AuditSummary.Count(items.Count)} 行、{AuditSummary.Money(order.TotalAmount)}）{AuditSummary.BatchItems(items, i => i.ProductName, i => i.BatchNo)}";
                await _auditLogger.RecordAsync(new AuditEntry
                {
                    Resource = AuditResource.SalesShipment,
                    Action = AuditAction.Create,
                    ResourceId = order.Id,
                    ResourceNo = order.ShipmentNo,
                    Summary = requiresApproval ? $"{createdSummary}｜已提交审批（审批通过后才产生库存变动）" : createdSummary,
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

                // 站内信在提交之后发送：发信失败只记日志，不影响已落库单据（042 §3.5）
                if (pendingApproval is not null)
                {
                    await _approvalNotifier.NotifyPendingAsync(pendingApproval, cancellationToken);
                }

                return SalesShipmentsDtoMapper.ToSalesShipmentDetailDto(createdOrder, createdItems, partner.PaymentTermDays);
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
