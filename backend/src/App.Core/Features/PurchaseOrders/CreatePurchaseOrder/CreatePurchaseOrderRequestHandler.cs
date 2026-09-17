using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.PurchaseOrders.CreatePurchaseOrder;

/// <summary>
/// 新增采购订单用例（计划单据）：
/// 供应商校验（存在 / 启用 / 类型含供应商）→ 商品逐行校验（存在 / 启用）
/// → 后端重算小计 / 总额（不信任前端传值）→ 单号生成（PO + yyyyMMdd + 序号，唯一索引冲突重试最多 3 次）
/// → 同一事务：插订单 + 明细。
/// **不触碰库存与库存流水**（design.md §1 / §3.4）。
/// </summary>
public sealed class CreatePurchaseOrderRequestHandler : IRequestHandler<CreatePurchaseOrderRequest, PurchaseOrderDetailDto>
{
    /// <summary>采购订单单号前缀（销售订单为 SO，见 design.md §3.3）</summary>
    private const string OrderNoPrefix = "PO";

    /// <summary>单号冲突重试上限（含首次）</summary>
    private const int MaxOrderNoAttempts = 3;

    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IPartnerRepository _partnerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// 初始化新增采购订单用例处理器
    /// </summary>
    public CreatePurchaseOrderRequestHandler(
        IPurchaseOrderRepository purchaseOrderRepository,
        IPartnerRepository partnerRepository,
        IProductRepository productRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
        _partnerRepository = partnerRepository;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    /// <summary>
    /// 处理新增采购订单请求
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PurchaseOrderDetailDto> HandleAsync(CreatePurchaseOrderRequest request, CancellationToken cancellationToken = default)
    {
        // 双保险：明细非空（Validator 已拦格式层）
        if (request.Items is null || request.Items.Count == 0)
        {
            throw new BusinessException(ErrorCode.OrderItemsEmpty, "订单明细不能为空");
        }

        // 查库约束：供应商存在 / 启用 / 类型含供应商（纯客户不可下采购订单）
        var partner = await _partnerRepository.GetByIdAsync(request.PartnerId, cancellationToken);
        if (partner is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "供应商不存在");
        }

        if (partner.Status == PartnerStatus.Disabled)
        {
            throw new BusinessException(ErrorCode.PartnerDisabled, "供应商已停用，不可用于下单");
        }

        if (partner.Type is not (PartnerType.Supplier or PartnerType.Both))
        {
            throw new BusinessException(ErrorCode.PartnerTypeMismatch, "往来单位类型与采购订单不匹配");
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
                    throw new BusinessException(ErrorCode.ProductDisabled, "商品已停用，不可用于下单");
                }

                products[line.ProductId] = product;
            }

            var subtotal = line.Quantity * line.UnitPrice;
            totalAmount += subtotal;
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();

        // 事务：插订单 + 明细；单号冲突（唯一索引）时回滚后重新生成单号重试
        for (var attempt = 1; ; attempt++)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var orderNo = await _purchaseOrderRepository.GenerateOrderNoAsync(OrderNoPrefix, request.OrderDate, cancellationToken);
                var order = new PurchaseOrder
                {
                    Id = Guid.NewGuid(),
                    OrderNo = orderNo,
                    PartnerId = partner.Id,
                    PartnerName = partner.Name,
                    OrderDate = request.OrderDate,
                    ExpectedDate = request.ExpectedDate,
                    TotalAmount = totalAmount,
                    FlowStatus = OrderFlowStatus.Pending,
                    Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim(),
                    CreatedAt = now,
                    UpdatedAt = now,
                    CreatedBy = operatorId,
                    UpdatedBy = operatorId,
                };

                // 明细行按请求顺序生成顺序 Guid（SequentialGuidGenerator，见 design.md §3.4），
                // 保证持久化顺序与请求顺序一致（仓储按 Id 排序还原明细顺序）
                var items = new List<PurchaseOrderItem>(request.Items.Count);
                foreach (var line in request.Items)
                {
                    var p = products[line.ProductId];
                    items.Add(new PurchaseOrderItem
                    {
                        Id = SequentialGuidGenerator.NewSequential(),
                        OrderId = order.Id,
                        ProductId = line.ProductId,
                        ProductName = p.Name,
                        Unit = p.Unit,
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice,
                        Subtotal = line.UnitPrice * line.Quantity,
                        FulfilledQuantity = 0,
                    });
                }

                await _purchaseOrderRepository.AddAsync(order, items, cancellationToken);

                await _unitOfWork.CommitAsync(cancellationToken);

                var (createdOrder, createdItems) = await _purchaseOrderRepository.GetDetailAsync(order.Id, cancellationToken);
                if (createdOrder is null)
                {
                    throw new BusinessException(ErrorCode.NotFound, "采购订单创建后读取失败");
                }

                return PurchaseOrdersDtoMapper.ToPurchaseOrderDetailDto(createdOrder, createdItems);
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
