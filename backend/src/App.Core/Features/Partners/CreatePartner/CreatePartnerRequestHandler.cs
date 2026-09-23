using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Partners.CreatePartner;

/// <summary>
/// 新增往来单位用例：校验名称唯一（大小写不敏感）→ 落库（默认启用）。
/// 单一仓储写由仓储自身持久化，无需 IUnitOfWork。
/// </summary>
public sealed class CreatePartnerRequestHandler : IRequestHandler<CreatePartnerRequest, PartnerDto>
{
    private readonly IPartnerRepository _partnerRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化新增往来单位用例处理器
    /// </summary>
    public CreatePartnerRequestHandler(
        IPartnerRepository partnerRepository,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _partnerRepository = partnerRepository;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理新增往来单位请求
    /// </summary>
    /// <param name="request">新增请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PartnerDto> HandleAsync(CreatePartnerRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();

        // 查库约束：名称唯一（大小写不敏感）
        if (await _partnerRepository.ExistsByNameAsync(name, null, cancellationToken))
        {
            throw new BusinessException(ErrorCode.PartnerNameExists, "往来单位名称已存在");
        }

        var now = DateTimeOffset.UtcNow;
        var operatorId = _currentUser.UserId();
        var partner = new Partner
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = request.Type,
            Contact = string.IsNullOrWhiteSpace(request.Contact) ? null : request.Contact.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim(),
            // 账期与信用额度（036）：新增时按入参落库，未传为 0（现结 / 不限）
            PaymentTermDays = request.PaymentTermDays,
            CreditLimit = request.CreditLimit,
            Status = PartnerStatus.Enabled,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        await _partnerRepository.AddAsync(partner, cancellationToken);

        var changeBuilder = new AuditChangeBuilder()
            .Add("name", "单位名称", null, partner.Name)
            .Add("type", "单位类型", null, AuditText.PartnerType(partner.Type))
            .Add("contact", "联系人", null, partner.Contact)
            .Add("phone", "联系电话", null, partner.Phone)
            .Add("address", "地址", null, partner.Address)
            .Add("remark", "备注", null, partner.Remark)
            .Add("paymentTermDays", "账期天数", null, AuditSummary.Count(partner.PaymentTermDays))
            .Add("creditLimit", "信用额度", null, AuditSummary.Money(partner.CreditLimit));
        await _auditLogger.RecordAsync(new AuditEntry
        {
            Resource = AuditResource.Partner,
            Action = AuditAction.Create,
            ResourceId = partner.Id,
            ResourceNo = partner.Name,
            Summary = $"新增往来单位 {partner.Name}",
            Changes = changeBuilder.Build(),
            ChangesTruncated = changeBuilder.Truncated,
            UtcNow = now,
        }, cancellationToken);

        return PartnerDtoMapper.ToPartnerDto(partner);
    }
}
