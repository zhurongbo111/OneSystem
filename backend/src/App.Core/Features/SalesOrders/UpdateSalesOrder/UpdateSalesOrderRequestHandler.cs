using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.SalesOrders.UpdateSalesOrder;

/// <summary>
/// 编辑销售订单用例（仅「待发货」状态可改，design.md §3.4）：
/// 取单（40400）→ 已作废（40104）→ 非「待发货」（40116）
/// → 客户校验、商品逐行校验与金额重算（同创建）→ 明细全量替换（累计执行量恒为 0）。
/// 不触碰库存与库存流水。
/// </summary>
public sealed class UpdateSalesOrderRequestHandler : IRequestHandler<UpdateSalesOrderRequest, SalesOrderDetailDto>
{
    private readonly ISalesOrderRepository _salesOrderRepository;
    private readonly IPartnerRepository _partnerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;

    /// <summary>
    /// 初始化编辑销售订单用例处理器
    /// </summary>
    public UpdateSalesOrderRequestHandler(
        ISalesOrderRepository salesOrderRepository,
        IPartnerRepository partnerRepository,
        IProductRepository productRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser)
    {
        _salesOrderRepository = salesOrderRepository;
        _partnerRepository = partnerRepository;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    /// <summary>
    /// 处理编辑销售订单请求
    /// </summary>
    /// <param name="request">编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<SalesOrderDetailDto> HandleAsync(UpdateSalesOrderRequest request, CancellationToken cancellationToken = default)
    {
        var (order, _) = await _salesOrderRepository.GetDetailAsync(request.Id, cancellationToken);
        if (order is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "销售订单不存在");
        }

        if (order.FlowStatus == OrderFlowStatus.Voided)
        {
            throw new BusinessException(ErrorCode.OrderVoided, "订单已作废，禁止再操作");
        }

        // 已开始发货（部分发货 / 已完成）或已关闭后改数量会让在途量与历史发货失真（design.md §5）
        if (order.FlowStatus != OrderFlowStatus.Pending)
        {
            throw new BusinessException(ErrorCode.OrderStateInvalid, "订单当前状态不允许编辑");
        }

        // 查库约束：客户存在 / 启用 / 类型含客户
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

        // 主表可改字段（订单号 / 创建审计字段不可改，保持原值）
        order.PartnerId = partner.Id;
        order.PartnerName = partner.Name;
        order.OrderDate = request.OrderDate;
        order.ExpectedDate = request.ExpectedDate;
        order.TotalAmount = totalAmount;
        order.Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();
        order.UpdatedAt = now;
        order.UpdatedBy = operatorId;

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

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _salesOrderRepository.UpdateAsync(order, items, cancellationToken);
            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        var (updatedOrder, updatedItems) = await _salesOrderRepository.GetDetailAsync(order.Id, cancellationToken);
        if (updatedOrder is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "销售订单不存在");
        }

        return SalesOrdersDtoMapper.ToSalesOrderDetailDto(updatedOrder, updatedItems);
    }
}
