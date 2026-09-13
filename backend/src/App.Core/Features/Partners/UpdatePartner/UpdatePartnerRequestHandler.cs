using App.Core.Abstractions;
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

    /// <summary>
    /// 初始化编辑往来单位用例处理器
    /// </summary>
    public UpdatePartnerRequestHandler(IPartnerRepository partnerRepository, ICurrentUser currentUser)
    {
        _partnerRepository = partnerRepository;
        _currentUser = currentUser;
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

        // 名称不可修改，保持原值不变
        partner.Type = request.Type;
        partner.Contact = string.IsNullOrWhiteSpace(request.Contact) ? null : request.Contact.Trim();
        partner.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        partner.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        partner.Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();
        partner.UpdatedAt = DateTimeOffset.UtcNow;
        partner.UpdatedBy = Guid.TryParse(_currentUser.Id, out var id) ? (Guid?)id : null;

        await _partnerRepository.UpdateAsync(partner, cancellationToken);
        return ToDto(partner);
    }

    /// <summary>
    /// 实体转出参模型
    /// </summary>
    private static PartnerDto ToDto(Partner p)
        => new()
        {
            Id = p.Id.ToString(),
            Name = p.Name,
            Type = (int)p.Type,
            Contact = p.Contact,
            Phone = p.Phone,
            Address = p.Address,
            Remark = p.Remark,
            Status = (int)p.Status,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
        };
}
