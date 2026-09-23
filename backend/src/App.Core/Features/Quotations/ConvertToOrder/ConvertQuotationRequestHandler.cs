using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Quotations.ConvertToOrder;

/// <summary>
/// 报价单转销售订单用例（design.md §0.3 / §3.4）：
/// 取单（40400）→ 非草稿（40167）→ **同一事务**内：创建销售订单（明细按报价单原样复制、订单日期 = 转单当天）
/// + 回写报价单（`ConvertedOrderId` / `ConvertedOrderNo` / 状态置 `Converted`）→ 提交。
/// 报价单**只能转一次**；转单**不动库存、不写流水、不产生应收**（订单本身是计划数据）。
/// </summary>
public sealed class ConvertQuotationRequestHandler : IRequestHandler<ConvertQuotationRequest, ConvertQuotationResultDto>
{
    /// <summary>销售订单单号前缀（与 `024` 销售订单域一致，见 ROADMAP §6.7）</summary>
    private const string SalesOrderNoPrefix = "SO";

    /// <summary>单号冲突重试上限（含首次）</summary>
    private const int MaxOrderNoAttempts = 3;

    private readonly IQuotationRepository _quotationRepository;
    private readonly ISalesOrderRepository _salesOrderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化报价单转销售订单用例处理器
    /// </summary>
    public ConvertQuotationRequestHandler(
        IQuotationRepository quotationRepository,
        ISalesOrderRepository salesOrderRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _quotationRepository = quotationRepository;
        _salesOrderRepository = salesOrderRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理报价单转销售订单请求
    /// </summary>
    /// <param name="request">转单请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<ConvertQuotationResultDto> HandleAsync(ConvertQuotationRequest request, CancellationToken cancellationToken = default)
    {
        var (quotation, quotationItems) = await _quotationRepository.GetDetailAsync(request.Id, cancellationToken);
        if (quotation is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "报价单不存在");
        }

        // 已转订单（防重复转单）/ 已作废均不可转（design.md §0.3）
        if (quotation.Status != QuotationStatus.Draft)
        {
            throw new BusinessException(ErrorCode.QuotationNotConvertible, "报价单不可转单（非草稿或已转订单）");
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();

        // 事务：建销售订单 + 回写报价单；订单号唯一索引冲突时回滚重试
        for (var attempt = 1; ; attempt++)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var orderNo = await _salesOrderRepository.GenerateOrderNoAsync(SalesOrderNoPrefix, now, cancellationToken);
                var order = new SalesOrder
                {
                    Id = Guid.NewGuid(),
                    OrderNo = orderNo,
                    PartnerId = quotation.PartnerId,
                    PartnerName = quotation.PartnerName,
                    OrderDate = now,
                    ExpectedDate = null,
                    TotalAmount = quotation.TotalAmount,
                    FlowStatus = OrderFlowStatus.Pending,
                    Remark = quotation.Remark,
                    CreatedAt = now,
                    UpdatedAt = now,
                    CreatedBy = operatorId,
                    UpdatedBy = operatorId,
                };

                // 明细按报价单原样复制（商品 / 数量 / 单价快照），累计已发数量恒为 0
                var orderItems = new List<SalesOrderItem>(quotationItems.Count);
                foreach (var item in quotationItems)
                {
                    orderItems.Add(new SalesOrderItem
                    {
                        Id = SequentialGuidGenerator.NewSequential(),
                        OrderId = order.Id,
                        ProductId = item.ProductId,
                        ProductName = item.ProductName,
                        Unit = item.Unit,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        Subtotal = item.Subtotal,
                        FulfilledQuantity = 0,
                    });
                }

                await _salesOrderRepository.AddAsync(order, orderItems, cancellationToken);

                // 回写报价单：转出的订单 id / 单号快照 + 状态置「已转订单」（锁定，不可再编辑 / 转单 / 作废）
                await _quotationRepository.UpdateStatusAsync(
                    quotation.Id, QuotationStatus.Converted, order.Id, order.OrderNo, operatorId, cancellationToken);

                var convertedChangeBuilder = new AuditChangeBuilder()
                    .Add("status", "报价单状态", AuditText.QuotationStatus(QuotationStatus.Draft), AuditText.QuotationStatus(QuotationStatus.Converted))
                    .Add("convertedOrderNo", "转出销售订单", null, order.OrderNo)
                    .Add("totalAmount", "报价金额", null, AuditSummary.Money(quotation.TotalAmount))
                    .Add("itemCount", "明细行数", null, AuditSummary.Count(orderItems.Count));
                await _auditLogger.RecordAsync(new AuditEntry
                {
                    Resource = AuditResource.Quotation,
                    Action = AuditAction.Update,
                    ResourceId = quotation.Id,
                    ResourceNo = quotation.QuotationNo,
                    Summary = $"报价单 {quotation.QuotationNo} 转销售订单 {order.OrderNo}（客户：{quotation.PartnerName}、{AuditSummary.Count(orderItems.Count)} 行、{AuditSummary.Money(quotation.TotalAmount)}）",
                    Changes = convertedChangeBuilder.Build(),
                    ChangesTruncated = convertedChangeBuilder.Truncated,
                    UtcNow = now,
                }, cancellationToken);

                await _unitOfWork.CommitAsync(cancellationToken);

                return QuotationsDtoMapper.ToConvertQuotationResultDto(order);
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
