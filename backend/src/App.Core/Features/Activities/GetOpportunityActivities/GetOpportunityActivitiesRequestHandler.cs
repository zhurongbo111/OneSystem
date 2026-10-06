using App.Core.Abstractions;
using App.Core.Entities;
using App.Core.Errors;

namespace App.Core.Features.Activities.GetOpportunityActivities;

/// <summary>
/// 商机跟进活动查询用例：商机存在性校验（40400）→ 按归属（商机 + id）取活动，跟进时间倒序
/// </summary>
public sealed class GetOpportunityActivitiesRequestHandler : IRequestHandler<GetOpportunityActivitiesRequest, IReadOnlyList<ActivityItemDto>>
{
    private readonly IOpportunityRepository _opportunityRepository;
    private readonly IActivityRepository _activityRepository;

    /// <summary>
    /// 初始化商机跟进活动查询用例处理器
    /// </summary>
    public GetOpportunityActivitiesRequestHandler(
        IOpportunityRepository opportunityRepository, IActivityRepository activityRepository)
    {
        _opportunityRepository = opportunityRepository;
        _activityRepository = activityRepository;
    }

    /// <summary>
    /// 处理商机跟进活动查询请求
    /// </summary>
    /// <param name="request">查询请求</param>
    /// <param name="cancellationToken">取消令牌</param>
    public async Task<IReadOnlyList<ActivityItemDto>> HandleAsync(GetOpportunityActivitiesRequest request, CancellationToken cancellationToken = default)
    {
        var opportunity = await _opportunityRepository.GetByIdAsync(request.OpportunityId, cancellationToken);
        if (opportunity is null)
        {
            throw new BusinessException(ErrorCode.NotFound, "商机不存在");
        }

        var items = await _activityRepository.GetByBizAsync(ActivityBizType.Opportunity, request.OpportunityId, cancellationToken);
        return items.Select(ActivitiesDtoMapper.ToActivityItemDto).ToList();
    }
}
