using App.Core.Abstractions;
using App.Core.Audit;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Partners.UpdatePartnerStatus;

/// <summary>
/// 往来单位停用 / 启用用例：校验存在 → 更新状态。
/// 停用后不可被新单据选择，但保留全部历史引用；启用后可再次被选择。
/// </summary>
public sealed class UpdatePartnerStatusRequestHandler : IRequestHandler<UpdatePartnerStatusRequest, PartnerDto>
{
    private readonly IPartnerRepository _partnerRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// 初始化往来单位停用 / 启用用例处理器
    /// </summary>
    public UpdatePartnerStatusRequestHandler(
        IPartnerRepository partnerRepository,
        ICurrentUser currentUser,
        IAuditLogger auditLogger)
    {
        _partnerRepository = partnerRepository;
        _currentUser = currentUser;
        _auditLogger = auditLogger;
    }

    /// <summary>
    /// 处理往来单位停用 / 启用请求
    /// </summary>
    /// <param name="request">停用 / 启用请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PartnerDto> HandleAsync(UpdatePartnerStatusRequest request, CancellationToken cancellationToken = default)
    {
        var partner = await _partnerRepository.GetByIdAsync(request.Id, cancellationToken);
        if (partner is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "往来单位不存在");
        }

        var beforeStatus = partner.Status;
        var now = DateTimeOffset.UtcNow;

        partner.Status = request.Status == (int)PartnerStatus.Disabled ? PartnerStatus.Disabled : PartnerStatus.Enabled;
        partner.UpdatedAt = now;
        partner.UpdatedBy = _currentUser.UserId();

        await _partnerRepository.UpdateAsync(partner, cancellationToken);

        var changeBuilder = new AuditChangeBuilder()
            .Add("status", "状态", AuditText.PartnerStatus(beforeStatus), AuditText.PartnerStatus(partner.Status));
        await _auditLogger.RecordAsync(new AuditEntry
        {
            Resource = AuditResource.Partner,
            Action = AuditAction.StatusChange,
            ResourceId = partner.Id,
            ResourceNo = partner.Name,
            Summary = $"{(partner.Status == PartnerStatus.Enabled ? "启用" : "停用")}往来单位 {partner.Name}",
            Changes = changeBuilder.Build(),
            ChangesTruncated = changeBuilder.Truncated,
            UtcNow = now,
        }, cancellationToken);

        return PartnerDtoMapper.ToPartnerDto(partner);
    }
}
