using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;
using App.Core.Features.Approvals;
using App.Core.Features.Batches;
using App.Core.Features.Warehouses;

namespace App.Core.Features.SalesReturns.CreateSalesReturn;

/// <summary>
/// 新增销售退货单用例（一步式）。
/// **未命中审批规则（042 §0.2）**：保存即生效 —— 客户校验（存在 / 启用 / 类型含客户）→ 商品逐行校验
///   → 后端重算小计 / 总额 → 单号生成（SR + yyyyMMdd + 序号，唯一索引冲突重试最多 3 次）
///   → 同一事务：<see cref="SalesReturnFulfillment"/> 生效（逐行库存回增 + 成本转回 + 流水 + 凭证）。
/// **命中审批规则**：单据落库为「待审批」并生成审批记录 + 给审批人发站内信，**不产生任何库存 / 流水 / 成本变化**；
///   生效动作延迟到审批通过时由同一 <see cref="SalesReturnFulfillment"/> 执行（specs/042-erp-approval）。
/// </summary>
public sealed class CreateSalesReturnRequestHandler : IRequestHandler<CreateSalesReturnRequest, SalesReturnDetailDto>
{
    /// <summary>销售退货单单号前缀（采购退货单为 PR，见 design.md §1 与 specs/ROADMAP.md §6.7）</summary>
    private const string ReturnNoPrefix = "SR";

    /// <summary>单号冲突重试上限（含首次）</summary>
    private const int MaxReturnNoAttempts = 3;

    private readonly ISalesReturnRepository _salesReturnRepository;
    private readonly IPartnerRepository _partnerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IBatchRepository _batchRepository;
    private readonly IApprovalRuleRepository _approvalRuleRepository;
    private readonly IApprovalRepository _approvalRepository;
    private readonly SalesReturnFulfillment _fulfillment;
    private readonly ApprovalNotifier _approvalNotifier;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;
    private readonly ISystemClock _clock;

    /// <summary>
    /// 初始化新增销售退货单用例处理器
    /// </summary>
    public CreateSalesReturnRequestHandler(
        ISalesReturnRepository salesReturnRepository,
        IPartnerRepository partnerRepository,
        IProductRepository productRepository,
        IWarehouseRepository warehouseRepository,
        IBatchRepository batchRepository,
        IApprovalRuleRepository approvalRuleRepository,
        IApprovalRepository approvalRepository,
        SalesReturnFulfillment fulfillment,
        ApprovalNotifier approvalNotifier,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger,
        ISystemClock clock)
    {
        _salesReturnRepository = salesReturnRepository;
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
    /// 处理新增销售退货单请求
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<SalesReturnDetailDto> HandleAsync(CreateSalesReturnRequest request, CancellationToken cancellationToken = default)
    {
        // 双保险：明细非空（Validator 已拦格式层）
        if (request.Items is null || request.Items.Count == 0)
        {
            throw new BusinessException(ErrorCode.OrderItemsEmpty, "单据明细不能为空");
        }

        // 查库约束：客户存在 / 启用 / 类型含客户（纯供应商不可开销售退货单）
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
            throw new BusinessException(ErrorCode.PartnerTypeMismatch, "往来单位类型与销售退货单不匹配");
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

        // 审批规则判定（042 §0.1）：规则启用 且 金额 ≥ 阈值 → 触发审批（保存时不生效）
        var rule = await _approvalRuleRepository.GetAsync(SettlementOrderType.SalesReturn, cancellationToken);
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
                var returnNo = await _salesReturnRepository.GenerateReturnNoAsync(ReturnNoPrefix, request.ReturnDate, cancellationToken);
                var salesReturn = new SalesReturn
                {
                    Id = Guid.NewGuid(),
                    ReturnNo = returnNo,
                    PartnerId = partner.Id,
                    PartnerName = partner.Name,
                    WarehouseId = warehouse.Id,
                    WarehouseName = warehouse.Name,
                    ReturnDate = request.ReturnDate,
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

                // 批次解析（040，事务内执行）：销售退货为入库类（outbound = false，过期批次不拦截），支持就地新建批次
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

                // 明细行按请求顺序生成顺序 Guid（SequentialGuidGenerator），
                // 保证持久化顺序与请求顺序一致（仓储按 Id 排序还原明细顺序）
                var items = new List<SalesReturnItem>(request.Items.Count);
                for (var index = 0; index < request.Items.Count; index++)
                {
                    var line = request.Items[index];
                    var p = products[line.ProductId];
                    var batch = resolvedBatches[index];
                    items.Add(new SalesReturnItem
                    {
                        Id = SequentialGuidGenerator.NewSequential(),
                        ReturnId = salesReturn.Id,
                        ProductId = line.ProductId,
                        ProductName = p.Name,
                        Unit = p.Unit,
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice,
                        Subtotal = line.UnitPrice * line.Quantity,
                        BatchId = batch?.BatchId,
                        BatchNo = batch?.BatchNo,
                    });
                }

                await _salesReturnRepository.AddAsync(salesReturn, items, cancellationToken);

                if (requiresApproval)
                {
                    // 命中审批：仅落单 + 审批记录，**不产生任何库存 / 流水 / 成本变化**（042 §0.2）
                    pendingApproval = new Approval
                    {
                        Id = Guid.NewGuid(),
                        OrderType = SettlementOrderType.SalesReturn,
                        OrderId = salesReturn.Id,
                        OrderNo = salesReturn.ReturnNo,
                        PartnerName = salesReturn.PartnerName,
                        Amount = salesReturn.TotalAmount,
                        Status = ApprovalStatus.Pending,
                        SubmittedBy = operatorId,
                        SubmittedAt = now,
                    };
                    await _approvalRepository.AddAsync(pendingApproval, cancellationToken);
                }
                else
                {
                    // 未命中审批：保存即生效（与改造前逐条一致，由共享生效组件执行）
                    await _fulfillment.ApplyAsync(salesReturn, items, operatorId, now, cancellationToken);
                }

                // 业务写成功后、提交前追加操作日志：与业务同事务，异常回滚则不产生日志
                var createdSalesReturnChangeBuilder = new AuditChangeBuilder()
                    .Add("returnNo", "退货单号", null, salesReturn.ReturnNo)
                    .Add("partnerName", "客户", null, salesReturn.PartnerName)
                    .Add("warehouseName", "入库仓", null, salesReturn.WarehouseName)
                    .Add("returnDate", "退货日期", null, AuditSummary.Date(salesReturn.ReturnDate))
                    .Add("totalAmount", "退货金额", null, AuditSummary.Money(salesReturn.TotalAmount))
                    .Add("remark", "备注", null, salesReturn.Remark);
                var createdSummary = $"创建销售退货单 {salesReturn.ReturnNo}（客户：{salesReturn.PartnerName}、入库仓：{salesReturn.WarehouseName}、{AuditSummary.Count(items.Count)} 行、{AuditSummary.Money(salesReturn.TotalAmount)}）{AuditSummary.BatchItems(items, i => i.ProductName, i => i.BatchNo)}";
                await _auditLogger.RecordAsync(new AuditEntry
                {
                    Resource = AuditResource.SalesReturn,
                    Action = AuditAction.Create,
                    ResourceId = salesReturn.Id,
                    ResourceNo = salesReturn.ReturnNo,
                    Summary = requiresApproval ? $"{createdSummary}｜已提交审批（审批通过后才产生库存变动）" : createdSummary,
                    Changes = createdSalesReturnChangeBuilder.Build(),
                    ChangesTruncated = createdSalesReturnChangeBuilder.Truncated,
                    UtcNow = now,
                }, cancellationToken);

                await _unitOfWork.CommitAsync(cancellationToken);

                var (createdReturn, createdItems) = await _salesReturnRepository.GetDetailAsync(salesReturn.Id, cancellationToken: cancellationToken);
                if (createdReturn is null)
                {
                    throw new BusinessException(ErrorCode.NotFound, "销售退货单创建后读取失败");
                }

                // 站内信在提交之后发送：发信失败只记日志，不影响已落库单据（042 §3.5）
                if (pendingApproval is not null)
                {
                    await _approvalNotifier.NotifyPendingAsync(pendingApproval, cancellationToken);
                }

                return SalesReturnsDtoMapper.ToSalesReturnDetailDto(createdReturn, createdItems);
            }
            catch (OrderNoConflictException) when (attempt < MaxReturnNoAttempts)
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
