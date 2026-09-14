using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Responses;

namespace App.Core.Features.Partners.GetPartners;

/// <summary>
/// 往来单位分页查询用例：关键词（名称 / 联系人模糊）+ 类型 + 状态筛选，创建时间倒序
/// </summary>
public sealed class GetPartnersRequestHandler : IRequestHandler<GetPartnersRequest, PagedResult<PartnerDto>>
{
    private readonly IPartnerRepository _partnerRepository;

    /// <summary>
    /// 初始化往来单位分页查询用例处理器
    /// </summary>
    public GetPartnersRequestHandler(IPartnerRepository partnerRepository)
    {
        _partnerRepository = partnerRepository;
    }

    /// <summary>
    /// 处理往来单位分页查询请求
    /// </summary>
    /// <param name="request">分页查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<PartnerDto>> HandleAsync(GetPartnersRequest request, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _partnerRepository.GetPagedAsync(
            request.Keyword,
            request.Type,
            request.Status,
            request.Page,
            request.PageSize,
            cancellationToken);

        return new PagedResult<PartnerDto>
        {
            Items = items.Select(PartnerDtoMapper.ToPartnerDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
