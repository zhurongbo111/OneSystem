using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Partners.UpdatePartner;

/// <summary>
/// 编辑往来单位用例：校验存在 → 更新类型 / 联系人 / 电话 / 地址 / 备注（不触碰 Name）。
/// 停用单位也允许编辑（修改信息后仍可停用）。
/// </summary>
public sealed class UpdatePartnerRequestHandler : IRequestHandler<UpdatePartnerRequest, PartnerDto>
{
    private readonly IPartnerRepository _partnerRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化编辑往来单位用例处理器
    /// </summary>
    public UpdatePartnerRequestHandler(
        IPartnerRepository partnerRepository,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _partnerRepository = partnerRepository;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理编辑往来单位请求
    /// </summary>
    /// <param name="request">编辑请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PartnerDto> HandleAsync(UpdatePartnerRequest request, CancellationToken cancellationToken = default)
    {
        var partner = await _partnerRepository.GetByIdAsync(request.Id, cancellationToken);
        if (partner is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "往来单位不存在");
        }

        // 类型只放宽不收窄：只允许保持原类型或改为两者，避免既有采购 / 销售单据因档案类型丢失而在编辑时校验失败
        if (request.Type != partner.Type && request.Type != PartnerType.Both)
        {
            throw new BusinessException(ErrorCode.PartnerTypeNarrowingNotAllowed, "单位类型只允许放宽（改为两者），不允许收窄");
        }

        var beforeType = partner.Type;
        var beforeContact = partner.Contact;
        var beforePhone = partner.Phone;
        var beforeAddress = partner.Address;
        var beforeRemark = partner.Remark;
        var now = DateTimeOffset.UtcNow;

        // 名称不可修改，保持原值不变
        partner.Type = request.Type;
        partner.Contact = string.IsNullOrWhiteSpace(request.Contact) ? null : request.Contact.Trim();
        partner.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        partner.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        partner.Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();
        partner.UpdatedAt = now;
        partner.UpdatedBy = _currentUser.UserId();

        await _partnerRepository.UpdateAsync(partner, cancellationToken);

        var changeBuilder = new AuditChangeBuilder()
            .Add("type", "单位类型", AuditText.PartnerType(beforeType), AuditText.PartnerType(partner.Type))
            .Add("contact", "联系人", beforeContact, partner.Contact)
            .Add("phone", "联系电话", beforePhone, partner.Phone)
            .Add("address", "地址", beforeAddress, partner.Address)
            .Add("remark", "备注", beforeRemark, partner.Remark);
        await _auditLogger.RecordAsync(new AuditEntry
        {
            Resource = AuditResource.Partner,
            Action = AuditAction.Update,
            ResourceId = partner.Id,
            ResourceNo = partner.Name,
            Summary = $"编辑往来单位 {partner.Name}",
            Changes = changeBuilder.Build(),
            ChangesTruncated = changeBuilder.Truncated,
            UtcNow = now,
        }, cancellationToken);

        return PartnerDtoMapper.ToPartnerDto(partner);
    }
}
