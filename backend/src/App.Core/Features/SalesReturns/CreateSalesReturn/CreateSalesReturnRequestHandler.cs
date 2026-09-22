using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.SalesReturns.CreateSalesReturn;

/// <summary>
/// 新增销售退货单用例（一步式：保存即生效）：
/// 客户校验（存在 / 启用 / 类型含客户）→ 商品逐行校验（存在 / 启用）
/// → 后端重算小计 / 总额（不信任前端传值）
/// → 同一事务：单号生成（SR + yyyyMMdd + 序号，唯一索引冲突重试最多 3 次）
///   → 插单 + 明细 → 逐行库存回增（IncrementAsync(+quantity)，无上限校验）+ 逐行流水（IUnitOfWork 包裹，见 design.md §3.5）。
/// 与采购退货的顺序差异：销售退货无库存约束，故「生成单号 → 插单 → 回增 + 流水」，
/// 不采用「先扣减再插单」（任一环节失败整体回滚）。
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
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化新增销售退货单用例处理器
    /// </summary>
    public CreateSalesReturnRequestHandler(
        ISalesReturnRepository salesReturnRepository,
        IPartnerRepository partnerRepository,
        IProductRepository productRepository,
        IInventoryRepository inventoryRepository,
        IStockMovementRepository stockMovementRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _salesReturnRepository = salesReturnRepository;
        _partnerRepository = partnerRepository;
        _productRepository = productRepository;
        _inventoryRepository = inventoryRepository;
        _stockMovementRepository = stockMovementRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
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

            var subtotal = line.Quantity * line.UnitPrice;
            totalAmount += subtotal;
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();

        // 事务：插单 + 明细 + 逐行库存回增 + 流水；单号冲突（唯一索引）时回滚后重新生成单号重试
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

                // 明细行按请求顺序生成顺序 Guid（SequentialGuidGenerator），
                // 保证持久化顺序与请求顺序一致（仓储按 Id 排序还原明细顺序）
                var items = new List<SalesReturnItem>(request.Items.Count);
                foreach (var line in request.Items)
                {
                    var p = products[line.ProductId];
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
                    });
                }

                await _salesReturnRepository.AddAsync(salesReturn, items, cancellationToken);

                // 库存回增 + 库存流水：销售退货入库无上限校验（design.md §5），与流水同事务逐行 1:1
                foreach (var item in items)
                {
                    await _inventoryRepository.IncrementAsync(item.ProductId, item.Quantity, cancellationToken);

                    // 成本（erp-cost design §0.2）：按被退销售单原出库成本单价退回；
                    // 销售退货不关联原单（specs/021 §5），查不到原流水时兜底按当前移动加权均价
                    var unitCost = await _stockMovementRepository.GetMovementUnitCostAsync(
                        salesReturn.Id, item.ProductId, StockMovementType.SalesOutbound, cancellationToken)
                        ?? await _inventoryRepository.GetAverageCostAsync(item.ProductId, cancellationToken);
                    var totalCost = CostCalculator.TotalCost(item.Quantity, unitCost);
                    await _inventoryRepository.ApplyInboundCostAsync(item.ProductId, item.Quantity, unitCost, cancellationToken);

                    await _stockMovementRepository.AppendAsync(new StockMovement
                    {
                        Id = Guid.NewGuid(),
                        ProductId = item.ProductId,
                        MovementType = StockMovementType.SalesReturnIn,
                        Quantity = item.Quantity,
                        UnitCost = unitCost,
                        TotalCost = totalCost,
                        SourceId = salesReturn.Id,
                        SourceNo = returnNo,
                        CreatedAt = now,
                        CreatedBy = operatorId,
                    }, cancellationToken);
                }

                // 业务写成功后、提交前追加操作日志：与业务同事务，异常回滚则不产生日志
                var createdSalesReturnChangeBuilder = new AuditChangeBuilder()
                    .Add("returnNo", "退货单号", null, salesReturn.ReturnNo)
                    .Add("partnerName", "客户", null, salesReturn.PartnerName)
                    .Add("returnDate", "退货日期", null, AuditSummary.Date(salesReturn.ReturnDate))
                    .Add("totalAmount", "退货金额", null, AuditSummary.Money(salesReturn.TotalAmount))
                    .Add("remark", "备注", null, salesReturn.Remark);
                await _auditLogger.RecordAsync(new AuditEntry
                {
                    Resource = AuditResource.SalesReturn,
                    Action = AuditAction.Create,
                    ResourceId = salesReturn.Id,
                    ResourceNo = salesReturn.ReturnNo,
                    Summary = $"创建销售退货单 {salesReturn.ReturnNo}（客户：{salesReturn.PartnerName}、{AuditSummary.Count(items.Count)} 行、{AuditSummary.Money(salesReturn.TotalAmount)}）",
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
