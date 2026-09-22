using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.PurchaseOrders.UpdatePurchaseOrder;

/// <summary>
/// 编辑采购订单用例（仅「待收货」状态可改，design.md §3.4）：
/// 取单（40400）→ 已作废（40104）→ 非「待收货」（40116）
/// → 供应商校验、商品逐行校验与金额重算（同创建）→ 明细全量替换（累计执行量恒为 0）。
/// 不触碰库存与库存流水。
/// </summary>
public sealed class UpdatePurchaseOrderRequestHandler : IRequestHandler<UpdatePurchaseOrderRequest, PurchaseOrderDetailDto>
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IPartnerRepository _partnerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化编辑采购订单用例处理器
    /// </summary>
    public UpdatePurchaseOrderRequestHandler(
        IPurchaseOrderRepository purchaseOrderRepository,
        IPartnerRepository partnerRepository,
        IProductRepository productRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
        _partnerRepository = partnerRepository;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理编辑采购订单请求
    /// </summary>
    /// <param name="request">编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PurchaseOrderDetailDto> HandleAsync(UpdatePurchaseOrderRequest request, CancellationToken cancellationToken = default)
    {
        var (order, beforeOrderItems) = await _purchaseOrderRepository.GetDetailAsync(request.Id, cancellationToken);
        if (order is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "采购订单不存在");
        }

        if (order.FlowStatus == OrderFlowStatus.Voided)
        {
            throw new BusinessException(ErrorCode.OrderVoided, "订单已作废，禁止再操作");
        }

        // 已开始收货（部分收货 / 已完成）或已关闭后改数量会让在途量与历史收货失真（design.md §5）
        if (order.FlowStatus != OrderFlowStatus.Pending)
        {
            throw new BusinessException(ErrorCode.OrderStateInvalid, "订单当前状态不允许编辑");
        }

        // 查库约束：供应商存在 / 启用 / 类型含供应商
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

        // 商品逐行校验 + 快照收集 + 金额重算
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

        // 变更前后快照：明细为全量替换，逐行差异在 Change 明细中以行数呈现
        var beforePartnerName = order.PartnerName;
        var beforeOrderDate = order.OrderDate;
        var beforeExpectedDate = order.ExpectedDate;
        var beforeTotalAmount = order.TotalAmount;
        var beforeRemark = order.Remark;
        var beforeItemCount = beforeOrderItems.Count;

        // 主表可改字段（订单号 / 创建审计字段不可改，保持原值）
        order.PartnerId = partner.Id;
        order.PartnerName = partner.Name;
        order.OrderDate = request.OrderDate;
        order.ExpectedDate = request.ExpectedDate;
        order.TotalAmount = totalAmount;
        order.Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();
        order.UpdatedAt = now;
        order.UpdatedBy = operatorId;

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

        var updatedOrderChangeBuilder = new AuditChangeBuilder()
            .Add("partnerName", "供应商", beforePartnerName, order.PartnerName)
            .Add("orderDate", "订单日期", AuditSummary.Date(beforeOrderDate), AuditSummary.Date(order.OrderDate))
            .Add("expectedDate", "预计收货日期", AuditSummary.Date(beforeExpectedDate), AuditSummary.Date(order.ExpectedDate))
            .Add("totalAmount", "订单金额", AuditSummary.Money(beforeTotalAmount), AuditSummary.Money(order.TotalAmount))
            .Add("itemCount", "明细行数", AuditSummary.Count(beforeItemCount), AuditSummary.Count(items.Count))
            .Add("remark", "备注", beforeRemark, order.Remark);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _purchaseOrderRepository.UpdateAsync(order, items, cancellationToken);

            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.PurchaseOrder,
                Action = AuditAction.Update,
                ResourceId = order.Id,
                ResourceNo = order.OrderNo,
                Summary = $"编辑采购订单 {order.OrderNo}（{AuditSummary.Count(beforeItemCount)} 行 → {AuditSummary.Count(items.Count)} 行、{AuditSummary.Money(beforeTotalAmount)} → {AuditSummary.Money(order.TotalAmount)}）",
                Changes = updatedOrderChangeBuilder.Build(),
                ChangesTruncated = updatedOrderChangeBuilder.Truncated,
                UtcNow = now,
            }, cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        var (updatedOrder, updatedItems) = await _purchaseOrderRepository.GetDetailAsync(order.Id, cancellationToken);
        if (updatedOrder is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "采购订单不存在");
        }

        return PurchaseOrdersDtoMapper.ToPurchaseOrderDetailDto(updatedOrder, updatedItems);
    }
}
