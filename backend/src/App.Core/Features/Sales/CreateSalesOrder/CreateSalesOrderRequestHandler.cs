using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Sales.CreateSalesOrder;

/// <summary>
/// 新增销售单用例（一步式：保存即生效，库存立即减少）：
/// 客户校验（存在 / 启用 / 类型含客户）→ 商品逐行校验（存在 / 启用）
/// → 后端重算小计 / 总额（不信任前端传值）
/// → 同一事务：逐行 TryDecrementAsync 扣库存（任一行不足 → 40103 回滚整单）
///   → 单号生成（SO + yyyyMMdd + 序号，唯一索引冲突重试最多 3 次）
///   → 插单 + 明细（IUnitOfWork 包裹，见 design.md §3.4）。
/// </summary>
public sealed class CreateSalesOrderRequestHandler : IRequestHandler<CreateSalesOrderRequest, SalesOrderDetailDto>
{
    /// <summary>销售单单号前缀（采购单为 PO，见 erp-sale design.md §0）</summary>
    private const string OrderNoPrefix = "SO";

    /// <summary>单号冲突重试上限（含首次）</summary>
    private const int MaxOrderNoAttempts = 3;

    private readonly ISalesOrderRepository _salesOrderRepository;
    private readonly IPartnerRepository _partnerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// 初始化新增销售单用例处理器
    /// </summary>
    public CreateSalesOrderRequestHandler(
        ISalesOrderRepository salesOrderRepository,
        IPartnerRepository partnerRepository,
        IProductRepository productRepository,
        IInventoryRepository inventoryRepository,
        IStockMovementRepository stockMovementRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser)
    {
        _salesOrderRepository = salesOrderRepository;
        _partnerRepository = partnerRepository;
        _productRepository = productRepository;
        _inventoryRepository = inventoryRepository;
        _stockMovementRepository = stockMovementRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    /// <summary>
    /// 处理新增销售单请求
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<SalesOrderDetailDto> HandleAsync(CreateSalesOrderRequest request, CancellationToken cancellationToken = default)
    {
        // 双保险：明细非空（Validator 已拦格式层）
        if (request.Items is null || request.Items.Count == 0)
        {
            throw new BusinessException(ErrorCode.OrderItemsEmpty, "单据明细不能为空");
        }

        // 查库约束：客户存在 / 启用 / 类型含客户（纯供应商不可开销售单）
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
            throw new BusinessException(ErrorCode.PartnerTypeMismatch, "往来单位类型与销售单不匹配");
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

        // 事务：先扣库存（防超卖），成功后再插单 + 明细；单号冲突（唯一索引）时回滚后重新生成单号重试。
        // 注意：重试意味着重新扣减——上一轮已扣库存会随回滚恢复，故重新生成单号后整轮重来是安全的。
        for (var attempt = 1; ; attempt++)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
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

                var orderNo = await _salesOrderRepository.GenerateOrderNoAsync(OrderNoPrefix, request.OrderDate, cancellationToken);
                var order = new SalesOrder
                {
                    Id = Guid.NewGuid(),
                    OrderNo = orderNo,
                    PartnerId = partner.Id,
                    PartnerName = partner.Name,
                    OrderDate = request.OrderDate,
                    TotalAmount = totalAmount,
                    SettlementStatus = OrderSettlementStatus.Unsettled,
                    Status = OrderStatus.Normal,
                    Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim(),
                    CreatedAt = now,
                    UpdatedAt = now,
                    CreatedBy = operatorId,
                    UpdatedBy = operatorId,
                };

                // 明细行按请求顺序生成顺序 Guid（SequentialGuidGenerator，见 erp-purchase design.md §3.1），
                // 保证持久化顺序与请求顺序一致（仓储按 Id 排序还原明细顺序）
                var items = new List<SalesOrderItem>(request.Items.Count);
                foreach (var line in request.Items)
                {
                    var p = products[line.ProductId];
                    items.Add(new SalesOrderItem
                    {
                        Id = SequentialGuidGenerator.NewSequential(),
                        OrderId = order.Id,
                        ProductId = line.ProductId,
                        ProductName = p.Name,
                        Unit = p.Unit,
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice,
                        Subtotal = line.UnitPrice * line.Quantity,
                    });
                }

                await _salesOrderRepository.AddAsync(order, items, cancellationToken);

                // 库存流水：销售出库，与库存扣减同事务（逐行 TryDecrementAsync 已全部成功后才走到这里）
                foreach (var item in items)
                {
                    await _stockMovementRepository.AppendAsync(new StockMovement
                    {
                        Id = Guid.NewGuid(),
                        ProductId = item.ProductId,
                        MovementType = StockMovementType.SalesOutbound,
                        Quantity = -item.Quantity,
                        SourceId = order.Id,
                        SourceNo = orderNo,
                        CreatedAt = now,
                        CreatedBy = operatorId,
                    }, cancellationToken);
                }

                await _unitOfWork.CommitAsync(cancellationToken);

                var (createdOrder, createdItems) = await _salesOrderRepository.GetDetailAsync(order.Id, cancellationToken);
                if (createdOrder is null)
                {
                    throw new BusinessException(ErrorCode.NotFound, "销售单创建后读取失败");
                }

                return SalesDtoMapper.ToSalesOrderDetailDto(createdOrder, createdItems);
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
