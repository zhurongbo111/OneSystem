using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Quotations.CreateQuotation;

/// <summary>
/// 新增报价单用例（纯意向单，design.md §3.4）：
/// 客户校验（存在 / 启用 / 类型含客户）→ 商品逐行校验（存在 / 启用）
/// → 后端重算小计 / 总额 → 单号生成（QT + yyyyMMdd + 序号，唯一索引冲突重试最多 3 次）
/// → 同一事务：插报价单 + 明细。
/// **不触碰库存、库存流水与收付款**。
/// </summary>
public sealed class CreateQuotationRequestHandler : IRequestHandler<CreateQuotationRequest, QuotationDetailDto>
{
    /// <summary>报价单号前缀（采购订单 PO / 销售订单 SO 之外的独立前缀，见 ROADMAP §6.7）</summary>
    private const string QuotationNoPrefix = "QT";

    /// <summary>单号冲突重试上限（含首次）</summary>
    private const int MaxQuotationNoAttempts = 3;

    private readonly IQuotationRepository _quotationRepository;
    private readonly IPartnerRepository _partnerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化新增报价单用例处理器
    /// </summary>
    public CreateQuotationRequestHandler(
        IQuotationRepository quotationRepository,
        IPartnerRepository partnerRepository,
        IProductRepository productRepository,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _quotationRepository = quotationRepository;
        _partnerRepository = partnerRepository;
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理新增报价单请求
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<QuotationDetailDto> HandleAsync(CreateQuotationRequest request, CancellationToken cancellationToken = default)
    {
        // 双保险：明细非空（Validator 已拦格式层）
        if (request.Items is null || request.Items.Count == 0)
        {
            throw new BusinessException(ErrorCode.OrderItemsEmpty, "报价单明细不能为空");
        }

        // 查库约束：客户存在 / 启用 / 类型含客户（与销售订单同口径，报价是订单前段）
        var partner = await _partnerRepository.GetByIdAsync(request.PartnerId, cancellationToken);
        if (partner is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "客户不存在");
        }

        if (partner.Status == PartnerStatus.Disabled)
        {
            throw new BusinessException(ErrorCode.PartnerDisabled, "客户已停用，不可用于报价");
        }

        if (partner.Type is not (PartnerType.Customer or PartnerType.Both))
        {
            throw new BusinessException(ErrorCode.PartnerTypeMismatch, "往来单位类型与报价单不匹配");
        }

        // 查库约束：商品逐行存在 / 启用；收集名称 / 单位快照，后端重算小计 / 总额（不信任前端传值）
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
                    throw new BusinessException(ErrorCode.ProductDisabled, "商品已停用，不可用于报价");
                }

                products[line.ProductId] = product;
            }

            totalAmount += line.Quantity * line.UnitPrice;
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();

        // 事务：插报价单 + 明细；单号冲突（唯一索引）时回滚后重新生成单号重试
        for (var attempt = 1; ; attempt++)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                var quotationNo = await _quotationRepository.GenerateNoAsync(QuotationNoPrefix, request.QuotationDate, cancellationToken);
                var quotation = new Quotation
                {
                    Id = Guid.NewGuid(),
                    QuotationNo = quotationNo,
                    PartnerId = partner.Id,
                    PartnerName = partner.Name,
                    QuotationDate = request.QuotationDate,
                    ValidUntil = request.ValidUntil,
                    TotalAmount = totalAmount,
                    Status = QuotationStatus.Draft,
                    Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim(),
                    CreatedAt = now,
                    UpdatedAt = now,
                    CreatedBy = operatorId,
                    UpdatedBy = operatorId,
                };

                // 明细行按请求顺序生成顺序 Guid（SequentialGuidGenerator，与 024 订单域一致）
                var items = new List<QuotationItem>(request.Items.Count);
                foreach (var line in request.Items)
                {
                    var p = products[line.ProductId];
                    items.Add(new QuotationItem
                    {
                        Id = SequentialGuidGenerator.NewSequential(),
                        QuotationId = quotation.Id,
                        ProductId = line.ProductId,
                        ProductName = p.Name,
                        Unit = p.Unit,
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice,
                        Subtotal = line.UnitPrice * line.Quantity,
                    });
                }

                await _quotationRepository.AddAsync(quotation, items, cancellationToken);

                var createdChangeBuilder = new AuditChangeBuilder()
                    .Add("quotationNo", "报价单号", null, quotation.QuotationNo)
                    .Add("partnerName", "客户", null, quotation.PartnerName)
                    .Add("quotationDate", "报价日期", null, AuditSummary.Date(quotation.QuotationDate))
                    .Add("validUntil", "有效期至", null, quotation.ValidUntil?.ToString("yyyy-MM-dd"))
                    .Add("itemCount", "明细行数", null, AuditSummary.Count(items.Count))
                    .Add("totalAmount", "报价金额", null, AuditSummary.Money(quotation.TotalAmount))
                    .Add("remark", "备注", null, quotation.Remark);
                await _auditLogger.RecordAsync(new AuditEntry
                {
                    Resource = AuditResource.Quotation,
                    Action = AuditAction.Create,
                    ResourceId = quotation.Id,
                    ResourceNo = quotation.QuotationNo,
                    Summary = $"创建报价单 {quotation.QuotationNo}（客户：{quotation.PartnerName}、{AuditSummary.Count(items.Count)} 行、{AuditSummary.Money(quotation.TotalAmount)}）",
                    Changes = createdChangeBuilder.Build(),
                    ChangesTruncated = createdChangeBuilder.Truncated,
                    UtcNow = now,
                }, cancellationToken);

                await _unitOfWork.CommitAsync(cancellationToken);

                var (created, createdItems) = await _quotationRepository.GetDetailAsync(quotation.Id, cancellationToken);
                if (created is null)
                {
                    throw new BusinessException(ErrorCode.NotFound, "报价单创建后读取失败");
                }

                return QuotationsDtoMapper.ToQuotationDetailDto(created, createdItems);
            }
            catch (OrderNoConflictException) when (attempt < MaxQuotationNoAttempts)
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
