using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.SalesOrders.CreateSalesOrder;

/// <summary>
/// 新增销售订单用例（计划单据）：
/// 客户校验（存在 / 启用 / 类型含客户）→ 商品逐行校验（存在 / 启用）
/// → 后端重算小计 / 总额（不信任前端传值）→ 单号生成（SO + yyyyMMdd + 序号，唯一索引冲突重试最多 3 次）
/// → 同一事务：插订单 + 明细。
/// **不触碰库存与库存流水**（design.md §1 / §3.4）。
/// </summary>
public sealed class CreateSalesOrderRequestHandler : IRequestHandler<CreateSalesOrderRequest, SalesOrderDetailDto>
{
    /// <summary>销售订单单号前缀（采购订单为 PO，见 design.md §3.3）</summary>
    private const string OrderNoPrefix = "SO";

    /// <summary>单号冲突重试上限（含首次）</summary>
    private const int MaxOrderNoAttempts = 3;

    private readonly ISalesOrderRepository _salesOrderRepository;
    private readonly IPartnerRepository _partnerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化新增销售订单用例处理器
    /// </summary>
    public CreateSalesOrderRequestHandler(
        ISalesOrderRepository salesOrderRepository,
        IPartnerRepository partnerRepository,
        IProductRepository productRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _salesOrderRepository = salesOrderRepository;
        _partnerRepository = partnerRepository;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理新增销售订单请求
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<SalesOrderDetailDto> HandleAsync(CreateSalesOrderRequest request, CancellationToken cancellationToken = default)
    {
        // 双保险：明细非空（Validator 已拦格式层）
        if (request.Items is null || request.Items.Count == 0)
        {
            throw new BusinessException(ErrorCode.OrderItemsEmpty, "订单明细不能为空");
        }

        // 查库约束：客户存在 / 启用 / 类型含客户（纯供应商不可下销售订单）
        var partner = await _partnerRepository.GetByIdAsync(request.PartnerId, cancellationToken);
        if (partner is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "客户不存在");
        }

        if (partner.Status == PartnerStatus.Disabled)
        {
            throw new BusinessException(ErrorCode.PartnerDisabled, "客户已停用，不可用于下单");
        }

        if (partner.Type is not (PartnerType.Customer or PartnerType.Both))
        {
            throw new BusinessException(ErrorCode.PartnerTypeMismatch, "往来单位类型与销售订单不匹配");
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

            totalAmount += line.Quantity * line.UnitPrice;
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();

        // 事务：插订单 + 明细；单号冲突（唯一索引）时回滚后重新生成单号重试
        for (var attempt = 1; ; attempt++)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var orderNo = await _salesOrderRepository.GenerateOrderNoAsync(OrderNoPrefix, request.OrderDate, cancellationToken);
                var order = new SalesOrder
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

                // 明细行按请求顺序生成顺序 Guid（SequentialGuidGenerator，见 design.md §3.4）
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
                        FulfilledQuantity = 0,
                    });
                }

                await _salesOrderRepository.AddAsync(order, items, cancellationToken);

                var createdOrderChangeBuilder = new AuditChangeBuilder()
                    .Add("orderNo", "订单号", null, order.OrderNo)
                    .Add("partnerName", "客户", null, order.PartnerName)
                    .Add("orderDate", "订单日期", null, AuditSummary.Date(order.OrderDate))
                    .Add("expectedDate", "预计发货日期", null, AuditSummary.Date(order.ExpectedDate))
                    .Add("itemCount", "明细行数", null, AuditSummary.Count(items.Count))
                    .Add("totalAmount", "订单金额", null, AuditSummary.Money(order.TotalAmount))
                    .Add("remark", "备注", null, order.Remark);
                await _auditLogger.RecordAsync(new AuditEntry
                {
                    Resource = AuditResource.SalesOrder,
                    Action = AuditAction.Create,
                    ResourceId = order.Id,
                    ResourceNo = order.OrderNo,
                    Summary = $"创建销售订单 {order.OrderNo}（客户：{order.PartnerName}、{AuditSummary.Count(items.Count)} 行、{AuditSummary.Money(order.TotalAmount)}）",
                    Changes = createdOrderChangeBuilder.Build(),
                    ChangesTruncated = createdOrderChangeBuilder.Truncated,
                    UtcNow = now,
                }, cancellationToken);

                await _unitOfWork.CommitAsync(cancellationToken);

                var (createdOrder, createdItems) = await _salesOrderRepository.GetDetailAsync(order.Id, cancellationToken);
                if (createdOrder is null)
                {
                    throw new BusinessException(ErrorCode.NotFound, "销售订单创建后读取失败");
                }

                return SalesOrdersDtoMapper.ToSalesOrderDetailDto(createdOrder, createdItems);
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
