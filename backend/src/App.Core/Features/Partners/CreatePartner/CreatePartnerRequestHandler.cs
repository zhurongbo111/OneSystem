using App.Core.Abstractions;
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

    /// <summary>
    /// 初始化新增往来单位用例处理器
    /// </summary>
    public CreatePartnerRequestHandler(IPartnerRepository partnerRepository, ICurrentUser currentUser)
    {
        _partnerRepository = partnerRepository;
        _currentUser = currentUser;
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
            Status = PartnerStatus.Enabled,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = operatorId,
            UpdatedBy = operatorId,
        };

        await _partnerRepository.AddAsync(partner, cancellationToken);
        return PartnerDtoMapper.ToPartnerDto(partner);
    }
}
