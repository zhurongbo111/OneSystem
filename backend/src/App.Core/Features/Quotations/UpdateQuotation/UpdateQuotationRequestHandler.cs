using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Quotations.UpdateQuotation;

/// <summary>
/// 编辑报价单用例（仅草稿可改，design.md §3.4）：
/// 取单（40400）→ 非草稿（40166）→ 客户 / 商品校验与金额重算（同创建）→ 明细全量替换。
/// 不触碰库存、库存流水与收付款。
/// </summary>
public sealed class UpdateQuotationRequestHandler : IRequestHandler<UpdateQuotationRequest, QuotationDetailDto>
{
    private readonly IQuotationRepository _quotationRepository;
    private readonly IPartnerRepository _partnerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化编辑报价单用例处理器
    /// </summary>
    public UpdateQuotationRequestHandler(
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
    /// 处理编辑报价单请求
    /// </summary>
    /// <param name="request">编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<QuotationDetailDto> HandleAsync(UpdateQuotationRequest request, CancellationToken cancellationToken = default)
    {
        var (quotation, beforeItems) = await _quotationRepository.GetDetailAsync(request.Id, cancellationToken);
        if (quotation is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "报价单不存在");
        }

        // 已转订单 / 已作废均为锁定态，不可再编辑（design.md §0.1）
        if (quotation.Status != QuotationStatus.Draft)
        {
            throw new BusinessException(ErrorCode.QuotationNotEditable, "报价单非草稿状态，不可编辑");
        }

        // 查库约束：客户存在 / 启用 / 类型含客户
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
                    throw new BusinessException(ErrorCode.ProductDisabled, "商品已停用，不可用于报价");
                }

                products[line.ProductId] = product;
            }

            totalAmount += line.Quantity * line.UnitPrice;
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();

        // 变更前后快照：明细为全量替换，逐行差异在 Change 明细中以行数呈现
        var beforePartnerName = quotation.PartnerName;
        var beforeQuotationDate = quotation.QuotationDate;
        var beforeValidUntil = quotation.ValidUntil;
        var beforeTotalAmount = quotation.TotalAmount;
        var beforeRemark = quotation.Remark;
        var beforeItemCount = beforeItems.Count;

        // 主表可改字段（报价单号 / 创建审计字段不可改，保持原值）
        quotation.PartnerId = partner.Id;
        quotation.PartnerName = partner.Name;
        quotation.QuotationDate = request.QuotationDate;
        quotation.ValidUntil = request.ValidUntil;
        quotation.TotalAmount = totalAmount;
        quotation.Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();
        quotation.UpdatedAt = now;
        quotation.UpdatedBy = operatorId;

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

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await _quotationRepository.UpdateAsync(quotation, items, cancellationToken);

            var updatedChangeBuilder = new AuditChangeBuilder()
                .Add("partnerName", "客户", beforePartnerName, quotation.PartnerName)
                .Add("quotationDate", "报价日期", AuditSummary.Date(beforeQuotationDate), AuditSummary.Date(quotation.QuotationDate))
                .Add("validUntil", "有效期至", beforeValidUntil?.ToString("yyyy-MM-dd"), quotation.ValidUntil?.ToString("yyyy-MM-dd"))
                .Add("totalAmount", "报价金额", AuditSummary.Money(beforeTotalAmount), AuditSummary.Money(quotation.TotalAmount))
                .Add("itemCount", "明细行数", AuditSummary.Count(beforeItemCount), AuditSummary.Count(items.Count))
                .Add("remark", "备注", beforeRemark, quotation.Remark);
            await _auditLogger.RecordAsync(new AuditEntry
            {
                Resource = AuditResource.Quotation,
                Action = AuditAction.Update,
                ResourceId = quotation.Id,
                ResourceNo = quotation.QuotationNo,
                Summary = $"编辑报价单 {quotation.QuotationNo}（{AuditSummary.Count(beforeItemCount)} 行 → {AuditSummary.Count(items.Count)} 行、{AuditSummary.Money(beforeTotalAmount)} → {AuditSummary.Money(quotation.TotalAmount)}）",
                Changes = updatedChangeBuilder.Build(),
                ChangesTruncated = updatedChangeBuilder.Truncated,
                UtcNow = now,
            }, cancellationToken);

            await _unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }

        var (updated, updatedItems) = await _quotationRepository.GetDetailAsync(quotation.Id, cancellationToken);
        if (updated is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "报价单不存在");
        }

        return QuotationsDtoMapper.ToQuotationDetailDto(updated, updatedItems);
    }
}
