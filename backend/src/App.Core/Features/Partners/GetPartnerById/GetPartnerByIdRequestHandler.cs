using App.Core.Abstractions;
using App.Core.Errors;

namespace App.Core.Features.Partners.GetPartnerById;

/// <summary>
/// 查询往来单位详情用例：不存在返回 40400
/// </summary>
public sealed class GetPartnerByIdRequestHandler : IRequestHandler<GetPartnerByIdRequest, PartnerDto>
{
    private readonly IPartnerRepository _partnerRepository;

    /// <summary>
    /// 初始化查询往来单位详情用例处理器
    /// </summary>
    public GetPartnerByIdRequestHandler(IPartnerRepository partnerRepository)
    {
        _partnerRepository = partnerRepository;
    }

    /// <summary>
    /// 处理查询往来单位详情请求
    /// </summary>
    /// <param name="request">查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PartnerDto> HandleAsync(GetPartnerByIdRequest request, CancellationToken cancellationToken = default)
    {
        var partner = await _partnerRepository.GetByIdAsync(request.Id, cancellationToken);
        if (partner is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "往来单位不存在");
        }

        return PartnerDtoMapper.ToPartnerDto(partner);
    }
}
