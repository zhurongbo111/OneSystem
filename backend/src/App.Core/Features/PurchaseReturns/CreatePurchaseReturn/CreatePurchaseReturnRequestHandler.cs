using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.PurchaseReturns.CreatePurchaseReturn;

/// <summary>
/// 新增采购退货单用例（一步式：保存即生效）：
/// 供应商校验（存在 / 启用 / 类型含供应商）→ 商品逐行校验（存在 / 启用）
/// → 后端重算小计 / 总额（不信任前端传值）
/// → 同一事务：逐行 TryDecrementAsync 扣库存（任一行不足 → 40103 回滚整单，退回的是实物，禁止负库存）
///   → 单号生成（PR + yyyyMMdd + 序号，唯一索引冲突重试最多 3 次）
///   → 插单 + 明细 + 逐行写流水（IUnitOfWork 包裹，见 design.md §3.4）。
/// </summary>
public sealed class CreatePurchaseReturnRequestHandler : IRequestHandler<CreatePurchaseReturnRequest, PurchaseReturnDetailDto>
{
    /// <summary>采购退货单单号前缀（销售退货单为 SR，见 design.md §0 与 specs/ROADMAP.md §6.7）</summary>
    private const string ReturnNoPrefix = "PR";

    /// <summary>单号冲突重试上限（含首次）</summary>
    private const int MaxReturnNoAttempts = 3;

    private readonly IPurchaseReturnRepository _purchaseReturnRepository;
    private readonly IPartnerRepository _partnerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化新增采购退货单用例处理器
    /// </summary>
    public CreatePurchaseReturnRequestHandler(
        IPurchaseReturnRepository purchaseReturnRepository,
        IPartnerRepository partnerRepository,
        IProductRepository productRepository,
        IInventoryRepository inventoryRepository,
        IStockMovementRepository stockMovementRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _purchaseReturnRepository = purchaseReturnRepository;
        _partnerRepository = partnerRepository;
        _productRepository = productRepository;
        _inventoryRepository = inventoryRepository;
        _stockMovementRepository = stockMovementRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理新增采购退货单请求
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PurchaseReturnDetailDto> HandleAsync(CreatePurchaseReturnRequest request, CancellationToken cancellationToken = default)
    {
        // 双保险：明细非空（Validator 已拦格式层）
        if (request.Items is null || request.Items.Count == 0)
        {
            throw new BusinessException(ErrorCode.OrderItemsEmpty, "单据明细不能为空");
        }

        // 查库约束：供应商存在 / 启用 / 类型含供应商（纯客户不可开采购退货单）
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
            throw new BusinessException(ErrorCode.PartnerTypeMismatch, "往来单位类型与采购退货单不匹配");
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

            var subtotal = line.Quantity * line.UnitPrice;
            totalAmount += subtotal;
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();

        // 事务：先扣库存（退回供应商的是实物，账上必须有货），成功后再插单 + 明细 + 流水；
        // 单号冲突（唯一索引）时回滚后重新生成单号重试——上一轮已扣库存随回滚恢复，整轮重来安全。
        for (var attempt = 1; ; attempt++)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                // 成本：退货出库按「变动前」移动加权均价结转（erp-cost design §0.2）—— 必须在扣减前读取
                var returnUnitCosts = new Dictionary<Guid, decimal>(request.Items.Count);
                foreach (var line in request.Items)
                {
                    returnUnitCosts[line.ProductId] =
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

                var returnNo = await _purchaseReturnRepository.GenerateReturnNoAsync(ReturnNoPrefix, request.ReturnDate, cancellationToken);
                var purchaseReturn = new PurchaseReturn
                {
                    Id = Guid.NewGuid(),
                    ReturnNo = returnNo,
                    PartnerId = partner.Id,
                    PartnerName = partner.Name,
                    ReturnDate = request.ReturnDate,
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
                var items = new List<PurchaseReturnItem>(request.Items.Count);
                foreach (var line in request.Items)
                {
                    var p = products[line.ProductId];
                    items.Add(new PurchaseReturnItem
                    {
                        Id = SequentialGuidGenerator.NewSequential(),
                        ReturnId = purchaseReturn.Id,
                        ProductId = line.ProductId,
                        ProductName = p.Name,
                        Unit = p.Unit,
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice,
                        Subtotal = line.UnitPrice * line.Quantity,
                    });
                }

                await _purchaseReturnRepository.AddAsync(purchaseReturn, items, cancellationToken);

                // 库存流水：采购退货出库（负方向），与库存扣减同事务（逐行 TryDecrementAsync 已全部成功后才走到这里）
                foreach (var item in items)
                {
                    // 成本：退货出库按变动前均价结转（erp-cost design §0.2）
                    var unitCost = returnUnitCosts[item.ProductId];
                    var totalCost = CostCalculator.TotalCost(item.Quantity, unitCost);
                    await _inventoryRepository.ApplyOutboundCostAsync(item.ProductId, totalCost, cancellationToken);

                    await _stockMovementRepository.AppendAsync(new StockMovement
                    {
                        Id = Guid.NewGuid(),
                        ProductId = item.ProductId,
                        MovementType = StockMovementType.PurchaseReturnOut,
                        Quantity = -item.Quantity,
                        UnitCost = unitCost,
                        TotalCost = -totalCost,
                        SourceId = purchaseReturn.Id,
                        SourceNo = returnNo,
                        CreatedAt = now,
                        CreatedBy = operatorId,
                    }, cancellationToken);
                }

                // 业务写成功后、提交前追加操作日志：与业务同事务，异常回滚则不产生日志
                var createdReturnChangeBuilder = new AuditChangeBuilder()
                    .Add("returnNo", "退货单号", null, purchaseReturn.ReturnNo)
                    .Add("partnerName", "供应商", null, purchaseReturn.PartnerName)
                    .Add("returnDate", "退货日期", null, AuditSummary.Date(purchaseReturn.ReturnDate))
                    .Add("totalAmount", "退货金额", null, AuditSummary.Money(purchaseReturn.TotalAmount))
                    .Add("remark", "备注", null, purchaseReturn.Remark);
                await _auditLogger.RecordAsync(new AuditEntry
                {
                    Resource = AuditResource.PurchaseReturn,
                    Action = AuditAction.Create,
                    ResourceId = purchaseReturn.Id,
                    ResourceNo = purchaseReturn.ReturnNo,
                    Summary = $"创建采购退货单 {purchaseReturn.ReturnNo}（供应商：{purchaseReturn.PartnerName}、{AuditSummary.Count(items.Count)} 行、{AuditSummary.Money(purchaseReturn.TotalAmount)}）",
                    Changes = createdReturnChangeBuilder.Build(),
                    ChangesTruncated = createdReturnChangeBuilder.Truncated,
                    UtcNow = now,
                }, cancellationToken);

                await _unitOfWork.CommitAsync(cancellationToken);

                var (createdReturn, createdItems) = await _purchaseReturnRepository.GetDetailAsync(purchaseReturn.Id, cancellationToken: cancellationToken);
                if (createdReturn is null)
                {
                    throw new BusinessException(ErrorCode.NotFound, "采购退货单创建后读取失败");
                }

                return PurchaseReturnsDtoMapper.ToPurchaseReturnDetailDto(createdReturn, createdItems);
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
