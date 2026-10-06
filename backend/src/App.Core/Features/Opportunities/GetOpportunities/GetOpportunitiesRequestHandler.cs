using App.Core.Abstractions;
using App.Core.Responses;

namespace App.Core.Features.Opportunities.GetOpportunities;

/// <summary>
/// 商机分页查询用例：仓储分页筛选（关键词 / 阶段 / 客户 / 负责人）→ 映射 DTO（含负责人姓名）
/// </summary>
public sealed class GetOpportunitiesRequestHandler : IRequestHandler<GetOpportunitiesRequest, PagedResult<OpportunityListItemDto>>
{
    private readonly IOpportunityRepository _opportunityRepository;

    /// <summary>
    /// 初始化商机分页查询用例处理器
    /// </summary>
    public GetOpportunitiesRequestHandler(IOpportunityRepository opportunityRepository)
    {
        _opportunityRepository = opportunityRepository;
    }

    /// <summary>
    /// 处理商机分页查询请求
    /// </summary>
    /// <param name="request">分页查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<PagedResult<OpportunityListItemDto>> HandleAsync(GetOpportunitiesRequest request, CancellationToken cancellationToken = default)
    {
        var (items, total) = await _opportunityRepository.GetPagedAsync(
            request.Keyword, request.Stage, request.PartnerId, request.OwnerId,
            request.Page, request.PageSize, cancellationToken);

        return new PagedResult<OpportunityListItemDto>
        {
            Items = items.Select(OpportunitiesDtoMapper.ToOpportunityListItemDto).ToList(),
            Total = total,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }
}
